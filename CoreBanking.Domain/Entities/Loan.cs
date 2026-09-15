namespace CoreBanking.Domain.Entities;

public class Loan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LoanNumber { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestRate { get; set; } // Annual percentage
    public int TenureMonths { get; set; }
    public decimal MonthlyRepayment { get; set; }
    public decimal OutstandingBalance { get; set; }
    public Enums.LoanStatus Status { get; set; } = Enums.LoanStatus.Pending;
    public int CreditScoreAtApplication { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? DisbursedAt { get; set; }
    public DateTime? DueDate { get; set; }
}