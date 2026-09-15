using CoreBanking.Application.DTOs;
using CoreBanking.Application.Interfaces;
using CoreBanking.Domain.Entities;
using CoreBanking.Domain.Enums;
using CoreBanking.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoreBanking.Application.Services;

public class LoanService : ILoanService
{
    private readonly IAccountRepository _accountRepo;
    private readonly ILoanRepository _loanRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICreditScoreService _creditScore;
    private readonly IEmailService _email;
    private readonly ILogger<LoanService> _logger;

    public LoanService(
        IAccountRepository accountRepo,
        ILoanRepository loanRepo,
        IUnitOfWork uow,
        ICreditScoreService creditScore,
        IEmailService email,
        ILogger<LoanService> logger)
    {
        _accountRepo = accountRepo;
        _loanRepo = loanRepo;
        _uow = uow;
        _creditScore = creditScore;
        _email = email;
        _logger = logger;
    }

    public async Task<LoanResponse> ApplyForLoanAsync(LoanApplicationRequest request, CancellationToken ct = default)
    {
        var account = await _accountRepo.GetByAccountNumberAsync(request.AccountNumber, ct)
            ?? throw new InvalidOperationException("Account not found");

        var score = await _creditScore.GetCreditScoreAsync(account.CustomerId, ct);
        var eligible = await _creditScore.IsEligibleForLoanAsync(account.CustomerId, request.PrincipalAmount, request.TenureMonths, ct);

        var monthlyRate = request.InterestRate / 100 / 12;
        var monthlyRepayment = request.PrincipalAmount * monthlyRate * (decimal)Math.Pow(1 + (double)monthlyRate, request.TenureMonths)
            / ((decimal)Math.Pow(1 + (double)monthlyRate, request.TenureMonths) - 1);

        var loan = new Loan
        {
            LoanNumber = $"LN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            AccountId = account.Id,
            PrincipalAmount = request.PrincipalAmount,
            InterestRate = request.InterestRate,
            TenureMonths = request.TenureMonths,
            MonthlyRepayment = Math.Round(monthlyRepayment, 2),
            OutstandingBalance = request.PrincipalAmount,
            CreditScoreAtApplication = score,
            Status = eligible ? LoanStatus.Approved : LoanStatus.Rejected,
            RejectionReason = eligible ? null : $"Credit score {score} below eligibility threshold or amount/tenure rules"
        };

        if (eligible)
        {
            loan.ApprovedAt = DateTime.UtcNow;
            loan.DueDate = DateTime.UtcNow.AddMonths(request.TenureMonths);
        }

        await _loanRepo.AddAsync(loan, ct);
        await _uow.SaveChangesAsync(ct);

        _ = _email.SendLoanDecisionAsync(account.Email, account.CustomerName, loan.LoanNumber,
            loan.Status.ToString(), loan.RejectionReason, ct);

        _logger.LogInformation("Loan {LoanNumber} application processed. Status: {Status}, Score: {Score}",
            loan.LoanNumber, loan.Status, score);

        return Map(loan);
    }

    public async Task<LoanResponse?> GetLoanAsync(Guid id, CancellationToken ct = default)
    {
        var loan = await _loanRepo.GetByIdAsync(id, ct);
        return loan == null ? null : Map(loan);
    }

    public async Task DisburseLoanAsync(Guid loanId, CancellationToken ct = default)
    {
        var loan = await _loanRepo.GetByIdAsync(loanId, ct)
            ?? throw new InvalidOperationException("Loan not found");

        if (loan.Status != LoanStatus.Approved)
            throw new InvalidOperationException("Only approved loans can be disbursed");

        var account = await _accountRepo.GetByIdAsync(loan.AccountId, ct)
            ?? throw new InvalidOperationException("Account not found");

        account.Balance += loan.PrincipalAmount;
        account.UpdatedAt = DateTime.UtcNow;
        loan.Status = LoanStatus.Disbursed;
        loan.DisbursedAt = DateTime.UtcNow;
        loan.OutstandingBalance = loan.PrincipalAmount;

        await _accountRepo.UpdateAsync(account, ct);
        await _loanRepo.UpdateAsync(loan, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Loan {LoanNumber} disbursed to account {Account}", loan.LoanNumber, account.AccountNumber);
    }

    private static LoanResponse Map(Loan loan) => new(
        loan.Id, loan.LoanNumber, loan.PrincipalAmount, loan.InterestRate,
        loan.TenureMonths, loan.MonthlyRepayment, loan.Status.ToString(),
        loan.CreditScoreAtApplication, loan.RejectionReason, loan.AppliedAt);
}