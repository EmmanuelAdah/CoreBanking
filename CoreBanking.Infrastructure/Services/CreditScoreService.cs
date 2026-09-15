using CoreBanking.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoreBanking.Infrastructure.Services;

/// <summary>
/// Mock credit score service. Replace with real bureau integration (CRC, CreditRegistry, etc.)
/// </summary>
public class CreditScoreService : ICreditScoreService
{
    private readonly ILogger<CreditScoreService> _logger;
    private static readonly Random _rnd = new();

    public CreditScoreService(ILogger<CreditScoreService> logger) => _logger = logger;

    public async Task<int> GetCreditScoreAsync(string customerId, CancellationToken ct = default)
    {
        // Simulated score 300-850
        await Task.Delay(50, ct); // simulate latency
        var score = _rnd.Next(300, 851);
        _logger.LogInformation("Credit score for {CustomerId}: {Score}", customerId, score);
        return score;
    }

    public async Task<bool> IsEligibleForLoanAsync(string customerId, decimal amount, int tenureMonths, CancellationToken ct = default)
    {
        var score = await GetCreditScoreAsync(customerId, ct);

        // Simple eligibility rules
        if (score < 550) return false;
        if (amount > 5_000_000 && score < 700) return false;
        if (tenureMonths > 36 && score < 650) return false;

        return true;
    }
}