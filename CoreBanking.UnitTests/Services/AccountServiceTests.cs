using CoreBanking.Application.Services;
using CoreBanking.Domain.Entities;
using CoreBanking.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace CoreBanking.UnitTests.Services;

public class AccountServiceTests
{
    private readonly Mock<IAccountRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly AccountService _sut;

    public AccountServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _sut = new AccountService(_repo.Object, _uow.Object);
    }

    [Fact]
    public async Task CreateAccountAsync_GeneratesAccountNumberAndPersists()
    {
        _repo.Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account a, CancellationToken _) => a);

        var result = await _sut.CreateAccountAsync("Jane Doe", "jane@bank.com", "Savings");

        result.CustomerName.Should().Be("Jane Doe");
        result.Email.Should().Be("jane@bank.com");
        result.AccountType.Should().Be("Savings");
        result.AccountNumber.Should().NotBeNullOrWhiteSpace();
        result.Balance.Should().Be(0);
        result.IsActive.Should().BeTrue();

        _repo.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAccountAsync_WhenFound_ReturnsMappedResponse()
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            AccountNumber = "3999888777",
            CustomerName = "John",
            Email = "john@bank.com",
            AccountType = Domain.Enums.AccountType.Current,
            Balance = 50_000m,
            Currency = "NGN",
            IsActive = true
        };

        _repo.Setup(r => r.GetByAccountNumberAsync("3999888777", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.GetAccountAsync("3999888777");

        result.Should().NotBeNull();
        result!.AccountNumber.Should().Be("3999888777");
        result.Balance.Should().Be(50_000m);
        result.AccountType.Should().Be("Current");
    }

    [Fact]
    public async Task GetAccountAsync_WhenMissing_ReturnsNull()
    {
        _repo.Setup(r => r.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        var result = await _sut.GetAccountAsync("000");

        result.Should().BeNull();
    }
}