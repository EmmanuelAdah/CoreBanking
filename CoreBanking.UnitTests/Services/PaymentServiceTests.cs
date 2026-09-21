using CoreBanking.Application.DTOs;
using CoreBanking.Application.Interfaces;
using CoreBanking.Application.Services;
using CoreBanking.Domain.Entities;
using CoreBanking.Domain.Enums;
using CoreBanking.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoreBanking.UnitTests.Services;

public class PaymentServiceTests
{
    private readonly Mock<IAccountRepository> _accounts = new();
    private readonly Mock<ITransactionRepository> _txs = new();
    private readonly Mock<IDisputeRepository> _disputes = new();
    private readonly Mock<IOutboxRepository> _outbox = new();
    private readonly Mock<IIdempotencyRepository> _idempotency = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IPaystackService> _paystack = new();
    private readonly Mock<IFraudDetectionService> _fraud = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IKafkaProducer> _kafka = new();
    private readonly Mock<ILogger<PaymentService>> _logger = new();
    private readonly PaymentService _sut;

    private readonly Account _account = new()
    {
        Id = Guid.NewGuid(),
        AccountNumber = "3123456789",
        CustomerName = "Test User",
        Email = "test@bank.com",
        Balance = 10_000m,
        IsActive = true
    };

    public PaymentServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _fraud.Setup(f => f.EvaluateAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (string?)null));

        _sut = new PaymentService(
            _accounts.Object, _txs.Object, _disputes.Object, _outbox.Object, _idempotency.Object,
            _uow.Object, _paystack.Object, _fraud.Object, _email.Object, _kafka.Object, _logger.Object);
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenAccountExists_ReturnsPaystackResponse()
    {
        _accounts.Setup(a => a.GetByAccountNumberAsync("3123456789", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_account);
        _txs.Setup(t => t.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t, CancellationToken _) => t);
        _txs.Setup(t => t.UpdateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paystack.Setup(p => p.InitializeTransactionAsync(
                It.IsAny<InitiatePaymentRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiatePaymentResponse("ref-1", "https://paystack/auth", "access", "initialized"));

        var request = new InitiatePaymentRequest("3123456789", 5_000m, "test@bank.com", "Top up");

        var result = await _sut.InitiatePaymentAsync(request, null);

        result.Reference.Should().Be("ref-1");
        result.AuthorizationUrl.Should().Contain("paystack");
        _txs.Verify(t => t.AddAsync(It.Is<Transaction>(x =>
            x.Amount == 5_000m &&
            x.AccountId == _account.Id &&
            x.Status == TransactionStatus.Pending), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenAccountMissing_Throws()
    {
        _accounts.Setup(a => a.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        var act = () => _sut.InitiatePaymentAsync(
            new InitiatePaymentRequest("000", 100m, "a@b.com", "x"), null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task InitiatePaymentAsync_WithIdempotencyKey_ReturnsExistingTransaction()
    {
        var existing = new Transaction
        {
            Reference = "existing-ref",
            IdempotencyKey = "idem-1",
            Status = TransactionStatus.Pending,
            Amount = 1_000m
        };

        _txs.Setup(t => t.GetByIdempotencyKeyAsync("idem-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _sut.InitiatePaymentAsync(
            new InitiatePaymentRequest("3123456789", 1_000m, "test@bank.com", "dup"),
            "idem-1");

        result.Reference.Should().Be("existing-ref");
        _paystack.Verify(p => p.InitializeTransactionAsync(
            It.IsAny<InitiatePaymentRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenFraudSuspected_StillCreatesTransaction()
    {
        _accounts.Setup(a => a.GetByAccountNumberAsync("3123456789", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_account);
        _fraud.Setup(f => f.EvaluateAsync("3123456789", 600_000m, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, "High value transaction"));
        _txs.Setup(t => t.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t, CancellationToken _) => t);
        _txs.Setup(t => t.UpdateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _paystack.Setup(p => p.InitializeTransactionAsync(It.IsAny<InitiatePaymentRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiatePaymentResponse("ref-fraud", "url", "code", "initialized"));

        var result = await _sut.InitiatePaymentAsync(
            new InitiatePaymentRequest("3123456789", 600_000m, "test@bank.com", "large"), null);

        result.Reference.Should().Be("ref-fraud");
        _txs.Verify(t => t.AddAsync(It.Is<Transaction>(x => x.IsFraudSuspected), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_WritesOutboxAndPublishesKafka()
    {
        _outbox.Setup(o => o.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _kafka.Setup(k => k.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var payload = new PaystackWebhookPayload(
            "charge.success",
            new PaystackWebhookData("ref-99", "success", 500000, "NGN", "card", "Approved", null));

        await _sut.ProcessWebhookAsync(payload);

        _outbox.Verify(o => o.AddAsync(It.Is<OutboxMessage>(m =>
            m.Type == "PaystackWebhook" &&
            m.Status == OutboxStatus.Pending), It.IsAny<CancellationToken>()), Times.Once);
        _kafka.Verify(k => k.PublishAsync("payment.webhooks", "ref-99", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefundAsync_WhenTransactionSuccessful_CallsPaystackAndUpdatesStatus()
    {
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            Reference = "ref-ok",
            PaystackReference = "psk_ref",
            AccountId = _account.Id,
            Amount = 2_000m,
            Status = TransactionStatus.Successful
        };

        _txs.Setup(t => t.GetByReferenceAsync("ref-ok", It.IsAny<CancellationToken>())).ReturnsAsync(tx);
        _txs.Setup(t => t.UpdateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _accounts.Setup(a => a.GetByIdAsync(_account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_account);
        _accounts.Setup(a => a.UpdateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _paystack.Setup(p => p.RefundTransactionAsync("psk_ref", null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.RefundAsync(new RefundRequest("ref-ok"));

        tx.Status.Should().Be(TransactionStatus.Refunded);
        _paystack.Verify(p => p.RefundTransactionAsync("psk_ref", null, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefundAsync_WhenNotSuccessful_Throws()
    {
        _txs.Setup(t => t.GetByReferenceAsync("ref-pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Transaction { Reference = "ref-pending", Status = TransactionStatus.Pending });

        var act = () => _sut.RefundAsync(new RefundRequest("ref-pending"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*successful*");
    }

    [Fact]
    public async Task CreateDisputeAsync_MarksTransactionDisputed()
    {
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            Reference = "ref-d",
            Status = TransactionStatus.Successful
        };

        _txs.Setup(t => t.GetByReferenceAsync("ref-d", It.IsAny<CancellationToken>())).ReturnsAsync(tx);
        _txs.Setup(t => t.UpdateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _disputes.Setup(d => d.AddAsync(It.IsAny<Dispute>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dispute d, CancellationToken _) => d);

        var result = await _sut.CreateDisputeAsync(new DisputeRequest("ref-d", "Unauthorized charge"));

        result.Reason.Should().Be("Unauthorized charge");
        result.Status.Should().Be(DisputeStatus.Open.ToString());
        tx.Status.Should().Be(TransactionStatus.Disputed);
    }
}