using System.Text.Json;
using CoreBanking.Application.DTOs;
using CoreBanking.Application.Interfaces;
using CoreBanking.Domain.Entities;
using CoreBanking.Domain.Enums;
using CoreBanking.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoreBanking.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IAccountRepository _accountRepo;
    private readonly ITransactionRepository _txRepo;
    private readonly IDisputeRepository _disputeRepo;
    private readonly IOutboxRepository _outboxRepo;
    private readonly IIdempotencyRepository _idempotencyRepo;
    private readonly IUnitOfWork _uow;
    private readonly IPaystackService _paystack;
    private readonly IFraudDetectionService _fraud;
    private readonly IEmailService _email;
    private readonly IKafkaProducer _kafka;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IAccountRepository accountRepo,
        ITransactionRepository txRepo,
        IDisputeRepository disputeRepo,
        IOutboxRepository outboxRepo,
        IIdempotencyRepository idempotencyRepo,
        IUnitOfWork uow,
        IPaystackService paystack,
        IFraudDetectionService fraud,
        IEmailService email,
        IKafkaProducer kafka,
        ILogger<PaymentService> logger)
    {
        _accountRepo = accountRepo;
        _txRepo = txRepo;
        _disputeRepo = disputeRepo;
        _outboxRepo = outboxRepo;
        _idempotencyRepo = idempotencyRepo;
        _uow = uow;
        _paystack = paystack;
        _fraud = fraud;
        _email = email;
        _kafka = kafka;
        _logger = logger;
    }

    public async Task<InitiatePaymentResponse> InitiatePaymentAsync(InitiatePaymentRequest request, string? idempotencyKey, CancellationToken ct = default)
    {
        // Idempotency check
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existing = await _txRepo.GetByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existing != null)
            {
                _logger.LogInformation("Idempotent hit for key {Key}", idempotencyKey);
                // Return previous response shape (simplified)
                return new InitiatePaymentResponse(existing.Reference, "", "", existing.Status.ToString());
            }
        }

        var account = await _accountRepo.GetByAccountNumberAsync(request.AccountNumber, ct)
            ?? throw new InvalidOperationException("Account not found");

        var reference = $"CBS-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];

        var (isSuspicious, fraudReason) = await _fraud.EvaluateAsync(request.AccountNumber, request.Amount, "paystack", ct);

        var tx = new Transaction
        {
            Reference = reference,
            IdempotencyKey = idempotencyKey,
            AccountId = account.Id,
            Amount = request.Amount,
            Currency = "NGN",
            Narration = request.Narration,
            Status = TransactionStatus.Pending,
            IsFraudSuspected = isSuspicious,
            FraudReason = fraudReason
        };

        await _txRepo.AddAsync(tx, ct);
        await _uow.SaveChangesAsync(ct);

        if (isSuspicious)
        {
            _logger.LogWarning("Transaction {Ref} flagged for fraud: {Reason}", reference, fraudReason);
            // Still allow but mark for review – or block depending on policy
        }

        var paystackResponse = await _paystack.InitializeTransactionAsync(request, reference, ct);

        tx.PaystackReference = paystackResponse.Reference;
        await _txRepo.UpdateAsync(tx, ct);
        await _uow.SaveChangesAsync(ct);

        return paystackResponse;
    }

    public async Task ProcessWebhookAsync(PaystackWebhookPayload payload, CancellationToken ct = default)
    {
        // Transactional Outbox pattern: persist the event first, then process async
        var outbox = new OutboxMessage
        {
            Type = "PaystackWebhook",
            Payload = JsonSerializer.Serialize(payload),
            Status = OutboxStatus.Pending
        };

        await _outboxRepo.AddAsync(outbox, ct);
        await _uow.SaveChangesAsync(ct);

        // Publish to Kafka for async processing (the outbox processor will also handle reliability)
        await _kafka.PublishAsync("payment.webhooks", payload.Data.Reference, JsonSerializer.Serialize(payload), ct);

        _logger.LogInformation("Webhook for {Ref} accepted into outbox + Kafka", payload.Data.Reference);
    }

    public async Task ProcessOutboxWebhookAsync(PaystackWebhookPayload payload, CancellationToken ct = default)
    {
        var tx = await _txRepo.GetByReferenceAsync(payload.Data.Reference, ct);
        if (tx == null)
        {
            _logger.LogWarning("Transaction not found for webhook {Ref}", payload.Data.Reference);
            return;
        }

        if (tx.Status is TransactionStatus.Successful or TransactionStatus.Failed)
        {
            _logger.LogInformation("Transaction {Ref} already in terminal state", tx.Reference);
            return;
        }

        if (payload.Data.Status == "success")
        {
            tx.Status = TransactionStatus.Successful;
            tx.Channel = payload.Data.Channel;
            tx.ProcessedAt = DateTime.UtcNow;

            var account = await _accountRepo.GetByIdAsync(tx.AccountId, ct);
            if (account != null)
            {
                account.Balance += tx.Amount;
                account.UpdatedAt = DateTime.UtcNow;
                await _accountRepo.UpdateAsync(account, ct);

                // Fire-and-forget email
                _ = _email.SendTransactionNotificationAsync(account.Email, account.CustomerName, tx.Reference, tx.Amount, "Successful", ct);
            }
        }
        else
        {
            tx.Status = TransactionStatus.Failed;
            tx.ProcessedAt = DateTime.UtcNow;
        }

        await _txRepo.UpdateAsync(tx, ct);
        await _uow.SaveChangesAsync(ct);

        await _kafka.PublishAsync("transactions.completed", tx.Reference, JsonSerializer.Serialize(new
        {
            tx.Reference,
            tx.Status,
            tx.Amount
        }), ct);
    }

    public async Task<TransactionResponse?> GetTransactionAsync(string reference, CancellationToken ct = default)
    {
        var tx = await _txRepo.GetByReferenceAsync(reference, ct);
        if (tx == null) return null;

        return new TransactionResponse(
            tx.Id, tx.Reference, tx.Amount, tx.Currency,
            tx.Status.ToString(), tx.Narration, tx.CreatedAt, tx.IsFraudSuspected);
    }

    public async Task RefundAsync(RefundRequest request, CancellationToken ct = default)
    {
        var tx = await _txRepo.GetByReferenceAsync(request.TransactionReference, ct)
            ?? throw new InvalidOperationException("Transaction not found");

        if (tx.Status != TransactionStatus.Successful)
            throw new InvalidOperationException("Only successful transactions can be refunded");

        var success = await _paystack.RefundTransactionAsync(tx.PaystackReference ?? tx.Reference, request.Amount, request.Reason, ct);
        if (!success) throw new InvalidOperationException("Paystack refund failed");

        tx.Status = TransactionStatus.Refunded;
        tx.UpdatedAt = DateTime.UtcNow;
        await _txRepo.UpdateAsync(tx, ct);

        var account = await _accountRepo.GetByIdAsync(tx.AccountId, ct);
        if (account != null)
        {
            var refundAmt = request.Amount ?? tx.Amount;
            account.Balance -= refundAmt;
            await _accountRepo.UpdateAsync(account, ct);
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<DisputeResponse> CreateDisputeAsync(DisputeRequest request, CancellationToken ct = default)
    {
        var tx = await _txRepo.GetByReferenceAsync(request.TransactionReference, ct)
            ?? throw new InvalidOperationException("Transaction not found");

        var dispute = new Dispute
        {
            TransactionId = tx.Id,
            Reason = request.Reason,
            CustomerNotes = request.CustomerNotes,
            Status = DisputeStatus.Open
        };

        await _disputeRepo.AddAsync(dispute, ct);
        tx.Status = TransactionStatus.Disputed;
        await _txRepo.UpdateAsync(tx, ct);
        await _uow.SaveChangesAsync(ct);

        return new DisputeResponse(dispute.Id, dispute.Status.ToString(), dispute.Reason, dispute.CreatedAt);
    }
}