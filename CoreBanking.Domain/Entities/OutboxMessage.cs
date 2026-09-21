namespace CoreBanking.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty; // e.g. "PaymentWebhook", "TransactionCompleted"
    public string Payload { get; set; } = string.Empty; // JSON
    public Enums.OutboxStatus Status { get; set; } = Enums.OutboxStatus.Pending;
    public int RetryCount { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
}