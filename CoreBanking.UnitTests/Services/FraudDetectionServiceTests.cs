using CoreBanking.Domain.Interfaces;
using CoreBanking.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoreBanking.UnitTests.Services;

public class FraudDetectionServiceTests
{
    private readonly FraudDetectionService _sut = new(
        new Mock<ITransactionRepository>().Object,
        new Mock<ILogger<FraudDetectionService>>().Object);

    [Theory]
    [InlineData(1000, false)]
    [InlineData(499_999, false)]
    [InlineData(500_000, true)]
    [InlineData(1_000_000, true)]
    public async Task EvaluateAsync_HighValueRule_Works(decimal amount, bool expectedSuspicious)
    {
        var (isSuspicious, reason) = await _sut.EvaluateAsync("3123456789", amount, "card");

        isSuspicious.Should().Be(expectedSuspicious);
        if (expectedSuspicious)
            reason.Should().NotBeNullOrEmpty();
        else
            reason.Should().BeNull();
    }
}