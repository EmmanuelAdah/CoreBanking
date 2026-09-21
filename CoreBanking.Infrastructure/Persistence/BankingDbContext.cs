using CoreBanking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoreBanking.Infrastructure.Persistence;

public class BankingDbContext : DbContext
{
    public BankingDbContext(DbContextOptions<BankingDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AccountNumber).IsUnique();
            e.Property(x => x.Balance).HasPrecision(18, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasOne(x => x.Account).WithMany(a => a.Transactions).HasForeignKey(x => x.AccountId);
        });

        modelBuilder.Entity<Loan>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.LoanNumber).IsUnique();
            e.Property(x => x.PrincipalAmount).HasPrecision(18, 2);
            e.Property(x => x.InterestRate).HasPrecision(5, 2);
            e.Property(x => x.MonthlyRepayment).HasPrecision(18, 2);
            e.Property(x => x.OutstandingBalance).HasPrecision(18, 2);
            e.HasOne(x => x.Account).WithMany(a => a.Loans).HasForeignKey(x => x.AccountId);
        });

        modelBuilder.Entity<Dispute>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RefundAmount).HasPrecision(18, 2);
            e.HasOne(x => x.Transaction).WithMany(t => t.Disputes).HasForeignKey(x => x.TransactionId);
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.BVN).IsUnique().HasFilter("\"BVN\" IS NOT NULL");
            e.HasIndex(x => x.RefreshToken);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Role).HasMaxLength(32);
            e.Property(x => x.PhoneNumber).HasMaxLength(15);
            e.Property(x => x.BVN).HasMaxLength(15);
        });

        modelBuilder.Entity<IdempotencyRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => x.ExpiresAt);
        });
    }
}