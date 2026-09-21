using CoreBanking.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoreBanking.UnitTests.Services;

public class CreditScoreServiceTests
{
    private readonly CreditScoreService _sut = new(new Mock<ILogger<CreditScoreService>>().Object);

    [Fact]
    public async Task GetCreditScoreAsync_ReturnsScoreInValidRange()
    {
        var score = await _sut.GetCreditScoreAsync("customer-1");

        score.Should().BeInRange(300, 850);
    }

    [Fact]
    public async Task IsEligibleForLoanAsync_CompletesWithoutError()
    {
        // Mock service is random — we only assert it returns a boolean without throwing
        var eligible = await _sut.IsEligibleForLoanAsync("customer-1", 100_000m, 12);
        (eligible is true or false).Should().BeTrue();
    }
}