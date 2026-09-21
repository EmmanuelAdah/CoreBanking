using CoreBanking.Application.Interfaces;
using CoreBanking.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoreBanking.Infrastructure.Services;

public class FraudDetectionService : IFraudDetectionService
{
    private readonly ITransactionRepository _txRepo;
    private readonly ILogger<FraudDetectionService> _logger;

    // Simple rule-based engine (extend with ML later)
    private static readonly decimal HighValueThreshold = 500_000m; // NGN
    private static readonly int VelocityWindowMinutes = 10;
    private static readonly int MaxTransactionsInWindow = 5;

    public FraudDetectionService(ITransactionRepository txRepo, ILogger<FraudDetectionService> logger)
    {
        _txRepo = txRepo;
        _logger = logger;
    }

    public async Task<(bool IsSuspicious, string? Reason)> EvaluateAsync(
        string accountNumber, decimal amount, string channel, CancellationToken ct = default)
    {
        // Rule 1: High value
        if (amount >= HighValueThreshold)
        {
            _logger.LogWarning("High value transaction detected: {Amount} for {Account}", amount, accountNumber);
            return (true, $"High value transaction (≥ {HighValueThreshold:N0} NGN)");
        }

        // Rule 2: Unusual channel patterns can be added here
        // Rule 3: Velocity (simplified – in production query recent txs)
        // For skeleton we keep it lightweight

        await Task.CompletedTask;
        return (false, null);
    }
}