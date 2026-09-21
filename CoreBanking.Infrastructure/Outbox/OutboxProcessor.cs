using System.Text.Json;
using CoreBanking.Application.DTOs;
using CoreBanking.Application.Interfaces;
using CoreBanking.Domain.Enums;
using CoreBanking.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoreBanking.Infrastructure.Outbox;

/// <summary>
/// Background service that polls the transactional outbox and processes pending messages.
/// Guarantees at-least-once delivery for webhooks and other side effects.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;
    private const int MaxRetries = 5;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox processor loop");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

        var messages = (await outboxRepo.GetPendingAsync(BatchSize, ct)).ToList();
        if (messages.Count == 0) return;

        foreach (var msg in messages)
        {
            try
            {
                msg.Status = OutboxStatus.Processing;
                await outboxRepo.UpdateAsync(msg, ct);
                await uow.SaveChangesAsync(ct);

                if (msg.Type == "PaystackWebhook")
                {
                    var payload = JsonSerializer.Deserialize<PaystackWebhookPayload>(msg.Payload);
                    if (payload != null)
                    {
                        // Cast to concrete to call internal processing method
                        if (paymentService is CoreBanking.Application.Services.PaymentService concrete)
                        {
                            await concrete.ProcessOutboxWebhookAsync(payload, ct);
                        }
                    }
                }

                msg.Status = OutboxStatus.Processed;
                msg.ProcessedAt = DateTime.UtcNow;
                await outboxRepo.UpdateAsync(msg, ct);
                await uow.SaveChangesAsync(ct);

                _logger.LogInformation("Outbox message {Id} processed successfully", msg.Id);
            }
            catch (Exception ex)
            {
                msg.RetryCount++;
                msg.Error = ex.Message;
                msg.Status = msg.RetryCount >= MaxRetries ? OutboxStatus.Failed : OutboxStatus.Failed;
                msg.NextRetryAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, msg.RetryCount)); // exponential backoff
                await outboxRepo.UpdateAsync(msg, ct);
                await uow.SaveChangesAsync(ct);

                _logger.LogError(ex, "Failed to process outbox message {Id}. Retry {Retry}", msg.Id, msg.RetryCount);
            }
        }
    }
}