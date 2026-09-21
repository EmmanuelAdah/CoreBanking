using CoreBanking.Domain.Entities;

namespace CoreBanking.Domain.Interfaces;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default);
    Task<Account> AddAsync(Account account, CancellationToken ct = default);
    Task UpdateAsync(Account account, CancellationToken ct = default);
}

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Transaction?> GetByReferenceAsync(string reference, CancellationToken ct = default);
    Task<Transaction?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default);
    Task<Transaction> AddAsync(Transaction transaction, CancellationToken ct = default);
    Task UpdateAsync(Transaction transaction, CancellationToken ct = default);
}

public interface ILoanRepository
{
    Task<Loan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Loan>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
    Task<Loan> AddAsync(Loan loan, CancellationToken ct = default);
    Task UpdateAsync(Loan loan, CancellationToken ct = default);
}

public interface IDisputeRepository
{
    Task<Dispute?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Dispute> AddAsync(Dispute dispute, CancellationToken ct = default);
    Task UpdateAsync(Dispute dispute, CancellationToken ct = default);
}

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
    Task<IEnumerable<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default);
    Task UpdateAsync(OutboxMessage message, CancellationToken ct = default);
}

public interface IIdempotencyRepository
{
    Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct = default);
    Task AddAsync(IdempotencyRecord record, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}