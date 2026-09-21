namespace CoreBanking.Domain.Enums;

public enum TransactionStatus
{
    Pending = 0,
    Processing = 1,
    Successful = 2,
    Failed = 3,
    Reversed = 4,
    Disputed = 5,
    Refunded = 6
}

public enum AccountType
{
    Savings = 0,
    Current = 1,
    Loan = 2,
    FixedDeposit = 3
}

public enum LoanStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Disbursed = 3,
    Active = 4,
    Closed = 5,
    Defaulted = 6
}

public enum DisputeStatus
{
    Open = 0,
    UnderReview = 1,
    Resolved = 2,
    Rejected = 3,
    Escalated = 4
}

public enum OutboxStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Failed = 3
}