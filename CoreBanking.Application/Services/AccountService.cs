using CoreBanking.Application.DTOs;
using CoreBanking.Application.Interfaces;
using CoreBanking.Domain.Entities;
using CoreBanking.Domain.Enums;
using CoreBanking.Domain.Interfaces;

namespace CoreBanking.Application.Services;

public class AccountService : IAccountService
{
    private readonly IAccountRepository _repo;
    private readonly IUnitOfWork _uow;

    public AccountService(IAccountRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<AccountResponse> CreateAccountAsync(string customerName, string email, string accountType, CancellationToken ct = default)
    {
        if (!Enum.TryParse<AccountType>(accountType, true, out var type))
            type = AccountType.Savings;

        var account = new Account
        {
            AccountNumber = GenerateAccountNumber(),
            CustomerId = Guid.NewGuid().ToString("N")[..12],
            CustomerName = customerName,
            Email = email,
            AccountType = type,
            Balance = 0,
            Currency = "NGN"
        };

        await _repo.AddAsync(account, ct);
        await _uow.SaveChangesAsync(ct);

        return new AccountResponse(
            account.Id, account.AccountNumber, account.CustomerName, account.Email,
            account.AccountType.ToString(), account.Balance, account.Currency, account.IsActive);
    }

    public async Task<AccountResponse?> GetAccountAsync(string accountNumber, CancellationToken ct = default)
    {
        var account = await _repo.GetByAccountNumberAsync(accountNumber, ct);
        if (account == null) return null;

        return new AccountResponse(
            account.Id, account.AccountNumber, account.CustomerName, account.Email,
            account.AccountType.ToString(), account.Balance, account.Currency, account.IsActive);
    }

    private static string GenerateAccountNumber()
        => $"3{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000, 999999)}";
}