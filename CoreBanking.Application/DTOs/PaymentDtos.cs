namespace CoreBanking.Application.DTOs;

public record InitiatePaymentRequest(
    string AccountNumber,
    decimal Amount,
    string Email,
    string Narration,
    string? CallbackUrl = null
);

public record InitiatePaymentResponse(
    string Reference,
    string AuthorizationUrl,
    string AccessCode,
    string Status
);

public record PaystackWebhookPayload(
    string Event,
    PaystackWebhookData Data
);

public record PaystackWebhookData(
    string Reference,
    string Status,
    decimal Amount,
    string Currency,
    string Channel,
    string? GatewayResponse,
    Dictionary<string, object>? Metadata
);

public record TransactionResponse(
    Guid Id,
    string Reference,
    decimal Amount,
    string Currency,
    string Status,
    string Narration,
    DateTime CreatedAt,
    bool IsFraudSuspected
);

public record RefundRequest(
    string TransactionReference,
    decimal? Amount = null, // null = full refund
    string Reason = "Customer requested refund"
);

public record DisputeRequest(
    string TransactionReference,
    string Reason,
    string? CustomerNotes = null
);

public record LoanApplicationRequest(
    string AccountNumber,
    decimal PrincipalAmount,
    int TenureMonths,
    decimal InterestRate = 15.0m
);

public record LoanResponse(
    Guid Id,
    string LoanNumber,
    decimal PrincipalAmount,
    decimal InterestRate,
    int TenureMonths,
    decimal MonthlyRepayment,
    string Status,
    int CreditScoreAtApplication,
    string? RejectionReason,
    DateTime AppliedAt
);

public record AccountResponse(
    Guid Id,
    string AccountNumber,
    string CustomerName,
    string Email,
    string AccountType,
    decimal Balance,
    string Currency,
    bool IsActive
);