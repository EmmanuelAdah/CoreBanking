namespace CoreBanking.Domain.Entities;

public class Dispute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public Transaction? Transaction { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? CustomerNotes { get; set; }
    public Enums.DisputeStatus Status { get; set; } = Enums.DisputeStatus.Open;
    public decimal? RefundAmount { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}