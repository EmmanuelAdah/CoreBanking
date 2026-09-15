namespace CoreBanking.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = string.Empty; // Unique business reference
    public string? IdempotencyKey { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public string Narration { get; set; } = string.Empty;
    public Enums.TransactionStatus Status { get; set; } = Enums.TransactionStatus.Pending;
    public string? PaystackReference { get; set; }
    public string? Channel { get; set; } // card, bank_transfer, etc.
    public string? Metadata { get; set; } // JSON
    public bool IsFraudSuspected { get; set; }
    public string? FraudReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();
}