using CoreBanking.Application.DTOs;

namespace CoreBanking.Application.Interfaces;

public interface IPaystackService
{
    Task<InitiatePaymentResponse> InitializeTransactionAsync(InitiatePaymentRequest request, string reference, CancellationToken ct = default);
    Task<bool> VerifyTransactionAsync(string reference, CancellationToken ct = default);
    Task<bool> RefundTransactionAsync(string reference, decimal? amount, string reason, CancellationToken ct = default);
}

public interface IFraudDetectionService
{
    Task<(bool IsSuspicious, string? Reason)> EvaluateAsync(string accountNumber, decimal amount, string channel, CancellationToken ct = default);
}

public interface ICreditScoreService
{
    Task<int> GetCreditScoreAsync(string customerId, CancellationToken ct = default);
    Task<bool> IsEligibleForLoanAsync(string customerId, decimal amount, int tenureMonths, CancellationToken ct = default);
}

public interface IEmailService
{
    Task SendTransactionNotificationAsync(string toEmail, string customerName, string reference, decimal amount, string status, CancellationToken ct = default);
    Task SendLoanDecisionAsync(string toEmail, string customerName, string loanNumber, string status, string? reason, CancellationToken ct = default);
    Task SendDisputeUpdateAsync(string toEmail, string reference, string status, CancellationToken ct = default);
}

public interface IKafkaProducer
{
    Task PublishAsync(string topic, string key, string message, CancellationToken ct = default);
}

public interface IPaymentService
{
    Task<InitiatePaymentResponse> InitiatePaymentAsync(InitiatePaymentRequest request, string? idempotencyKey, CancellationToken ct = default);
    Task ProcessWebhookAsync(PaystackWebhookPayload payload, CancellationToken ct = default);
    Task<TransactionResponse?> GetTransactionAsync(string reference, CancellationToken ct = default);
    Task RefundAsync(RefundRequest request, CancellationToken ct = default);
    Task<DisputeResponse> CreateDisputeAsync(DisputeRequest request, CancellationToken ct = default);
}

public record DisputeResponse(Guid Id, string Status, string Reason, DateTime CreatedAt);

public interface ILoanService
{
    Task<LoanResponse> ApplyForLoanAsync(LoanApplicationRequest request, CancellationToken ct = default);
    Task<LoanResponse?> GetLoanAsync(Guid id, CancellationToken ct = default);
    Task DisburseLoanAsync(Guid loanId, CancellationToken ct = default);
}

public interface IAccountService
{
    Task<AccountResponse> CreateAccountAsync(string customerName, string email, string accountType, CancellationToken ct = default);
    Task<AccountResponse?> GetAccountAsync(string accountNumber, CancellationToken ct = default);
}

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(Guid userId, CancellationToken ct = default);
}

public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId, string email, string role, string fullName);
    string GenerateRefreshToken();
    System.Security.Claims.ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
