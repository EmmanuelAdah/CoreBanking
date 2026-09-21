using CoreBanking.Application.DTOs;
using CoreBanking.Application.Interfaces;
using CoreBanking.Application.Services;
using CoreBanking.Domain.Entities;
using CoreBanking.Domain.Enums;
using CoreBanking.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoreBanking.UnitTests.Services;

public class LoanServiceTests
{
    private readonly Mock<IAccountRepository> _accounts = new();
    private readonly Mock<ILoanRepository> _loans = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICreditScoreService> _credit = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<ILogger<LoanService>> _logger = new();
    private readonly LoanService _sut;

    private readonly Account _account = new()
    {
        Id = Guid.NewGuid(),
        AccountNumber = "3001112222",
        CustomerId = "cust-1",
        CustomerName = "Loan Applicant",
        Email = "loan@bank.com",
        Balance = 0
    };

    public LoanServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _email.Setup(e => e.SendLoanDecisionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _sut = new LoanService(_accounts.Object, _loans.Object, _uow.Object, _credit.Object, _email.Object, _logger.Object);
    }

    [Fact]
    public async Task ApplyForLoanAsync_WhenEligible_ApprovesLoan()
    {
        _accounts.Setup(a => a.GetByAccountNumberAsync("3001112222", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_account);
        _credit.Setup(c => c.GetCreditScoreAsync("cust-1", It.IsAny<CancellationToken>())).ReturnsAsync(720);
        _credit.Setup(c => c.IsEligibleForLoanAsync("cust-1", 500_000m, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _loans.Setup(l => l.AddAsync(It.IsAny<Loan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Loan l, CancellationToken _) => l);

        var result = await _sut.ApplyForLoanAsync(new LoanApplicationRequest("3001112222", 500_000m, 12, 15m));

        result.Status.Should().Be(LoanStatus.Approved.ToString());
        result.CreditScoreAtApplication.Should().Be(720);
        result.RejectionReason.Should().BeNull();
        result.MonthlyRepayment.Should().BeGreaterThan(0);
        result.LoanNumber.Should().StartWith("LN-");
    }

    [Fact]
    public async Task ApplyForLoanAsync_WhenNotEligible_RejectsLoan()
    {
        _accounts.Setup(a => a.GetByAccountNumberAsync("3001112222", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_account);
        _credit.Setup(c => c.GetCreditScoreAsync("cust-1", It.IsAny<CancellationToken>())).ReturnsAsync(480);
        _credit.Setup(c => c.IsEligibleForLoanAsync("cust-1", 2_000_000m, 24, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _loans.Setup(l => l.AddAsync(It.IsAny<Loan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Loan l, CancellationToken _) => l);

        var result = await _sut.ApplyForLoanAsync(new LoanApplicationRequest("3001112222", 2_000_000m, 24));

        result.Status.Should().Be(LoanStatus.Rejected.ToString());
        result.RejectionReason.Should().NotBeNullOrEmpty();
        result.CreditScoreAtApplication.Should().Be(480);
    }

    [Fact]
    public async Task ApplyForLoanAsync_WhenAccountMissing_Throws()
    {
        _accounts.Setup(a => a.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        var act = () => _sut.ApplyForLoanAsync(new LoanApplicationRequest("000", 1000m, 6));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task DisburseLoanAsync_WhenApproved_CreditsAccount()
    {
        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            LoanNumber = "LN-TEST",
            AccountId = _account.Id,
            PrincipalAmount = 100_000m,
            Status = LoanStatus.Approved,
            OutstandingBalance = 100_000m
        };

        _loans.Setup(l => l.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(loan);
        _loans.Setup(l => l.UpdateAsync(It.IsAny<Loan>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _accounts.Setup(a => a.GetByIdAsync(_account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_account);
        _accounts.Setup(a => a.UpdateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _sut.DisburseLoanAsync(loan.Id);

        loan.Status.Should().Be(LoanStatus.Disbursed);
        loan.DisbursedAt.Should().NotBeNull();
        _account.Balance.Should().Be(100_000m);
    }

    [Fact]
    public async Task DisburseLoanAsync_WhenNotApproved_Throws()
    {
        var loan = new Loan { Id = Guid.NewGuid(), Status = LoanStatus.Rejected };
        _loans.Setup(l => l.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(loan);

        var act = () => _sut.DisburseLoanAsync(loan.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*approved*");
    }
}