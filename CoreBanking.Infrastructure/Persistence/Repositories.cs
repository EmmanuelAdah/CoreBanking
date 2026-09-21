using CoreBanking.Domain.Entities;
using CoreBanking.Domain.Interfaces;
using CoreBanking.Domain.Entities;
using CoreBanking.Domain.Enums;
using CoreBanking.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CoreBanking.Infrastructure.Persistence;

public class AccountRepository : IAccountRepository
{
    private readonly BankingDbContext _db;
    public AccountRepository(BankingDbContext db) => _db = db;

    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Accounts.FindAsync(new object[] { id }, ct);

    public async Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
        => await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber, ct);

    public async Task<Account> AddAsync(Account account, CancellationToken ct = default)
    {
        _db.Accounts.Add(account);
        return account;
    }

    public Task UpdateAsync(Account account, CancellationToken ct = default)
    {
        _db.Accounts.Update(account);
        return Task.CompletedTask;
    }
}

public class TransactionRepository : ITransactionRepository
{
    private readonly BankingDbContext _db;
    public TransactionRepository(BankingDbContext db) => _db = db;

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Transactions.FindAsync(new object[] { id }, ct);

    public async Task<Transaction?> GetByReferenceAsync(string reference, CancellationToken ct = default)
        => await _db.Transactions.FirstOrDefaultAsync(t => t.Reference == reference, ct);

    public async Task<Transaction?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default)
        => await _db.Transactions.FirstOrDefaultAsync(t => t.IdempotencyKey == key, ct);

    public async Task<Transaction> AddAsync(Transaction transaction, CancellationToken ct = default)
    {
        _db.Transactions.Add(transaction);
        return transaction;
    }

    public Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        _db.Transactions.Update(transaction);
        return Task.CompletedTask;
    }
}

public class LoanRepository : ILoanRepository
{
    private readonly BankingDbContext _db;
    public LoanRepository(BankingDbContext db) => _db = db;

    public async Task<Loan?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Loans.FindAsync(new object[] { id }, ct);

    public async Task<IEnumerable<Loan>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
        => await _db.Loans.Where(l => l.AccountId == accountId).ToListAsync(ct);

    public async Task<Loan> AddAsync(Loan loan, CancellationToken ct = default)
    {
        _db.Loans.Add(loan);
        return loan;
    }

    public Task UpdateAsync(Loan loan, CancellationToken ct = default)
    {
        _db.Loans.Update(loan);
        return Task.CompletedTask;
    }
}

public class DisputeRepository : IDisputeRepository
{
    private readonly BankingDbContext _db;
    public DisputeRepository(BankingDbContext db) => _db = db;

    public async Task<Dispute?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Disputes.FindAsync(new object[] { id }, ct);

    public async Task<Dispute> AddAsync(Dispute dispute, CancellationToken ct = default)
    {
        _db.Disputes.Add(dispute);
        return dispute;
    }

    public Task UpdateAsync(Dispute dispute, CancellationToken ct = default)
    {
        _db.Disputes.Update(dispute);
        return Task.CompletedTask;
    }
}

public class OutboxRepository : IOutboxRepository
{
    private readonly BankingDbContext _db;
    public OutboxRepository(BankingDbContext db) => _db = db;

    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _db.OutboxMessages.Add(message);
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default)
        => await _db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending || 
                       (m.Status == OutboxStatus.Failed && m.NextRetryAt <= DateTime.UtcNow))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public Task UpdateAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _db.OutboxMessages.Update(message);
        return Task.CompletedTask;
    }
}

public class IdempotencyRepository : IIdempotencyRepository
{
    private readonly BankingDbContext _db;
    public IdempotencyRepository(BankingDbContext db) => _db = db;

    public async Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct = default)
        => await _db.IdempotencyRecords.FirstOrDefaultAsync(r => r.Key == key && r.ExpiresAt > DateTime.UtcNow, ct);

    public async Task AddAsync(IdempotencyRecord record, CancellationToken ct = default)
    {
        _db.IdempotencyRecords.Add(record);
        await Task.CompletedTask;
    }
}

public class UnitOfWork : IUnitOfWork
{
    private readonly BankingDbContext _db;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _transaction;

    public UnitOfWork(BankingDbContext db) => _db = db;

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _db.Database.BeginTransactionAsync(ct);

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
public class UserRepository : IUserRepository
{
    private readonly BankingDbContext _db;
    public UserRepository(BankingDbContext db) => _db = db;

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Users.FindAsync(new object[] { id }, ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        => await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, ct);

    public async Task<User> AddAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Add(user);
        return user;
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Update(user);
        return Task.CompletedTask;
    }
}
