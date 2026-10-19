using Microsoft.EntityFrameworkCore;
using PaymentProcessingAPI.Entities;

namespace PaymentProcessingAPI.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<PaymentStatusHistory> PaymentStatusHistory => Set<PaymentStatusHistory>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(account => account.Id);
            entity.HasIndex(account => account.AccountNumber).IsUnique();
            entity.Property(account => account.AccountNumber).HasMaxLength(64).IsRequired();
            entity.Property(account => account.CurrencyCode).IsRequired();
            entity.Property(account => account.Balance).HasPrecision(18, 2);
            entity.Property(account => account.ReservedBalance).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(account => account.CreatedAtUtc).IsRequired();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Accounts_CurrencyCode", "\"CurrencyCode\" BETWEEN 1 AND 999");
                table.HasCheckConstraint("CK_Accounts_Balance_NonNegative", "\"Balance\" >= 0");
                table.HasCheckConstraint("CK_Accounts_ReservedBalance_Valid", "\"ReservedBalance\" >= 0 AND \"ReservedBalance\" <= \"Balance\"");
            });
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(payment => payment.Id);
            entity.HasIndex(payment => payment.TransactionId).IsUnique();
            entity.Property(payment => payment.TransactionId).HasMaxLength(100).IsRequired();
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.CurrencyCode).IsRequired();
            entity.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(payment => payment.ExternalReference).HasMaxLength(200);
            entity.Property(payment => payment.FailureReason).HasMaxLength(1000);
            entity.Property(payment => payment.CreatedAtUtc).IsRequired();
            entity.Property(payment => payment.UpdatedAtUtc).IsRequired();
            entity.HasOne(payment => payment.SenderAccount)
                .WithMany(account => account.SentPayments)
                .HasForeignKey(payment => payment.SenderAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(payment => payment.ReceiverAccount)
                .WithMany(account => account.ReceivedPayments)
                .HasForeignKey(payment => payment.ReceiverAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Payments_Amount_Positive", "\"Amount\" > 0");
                table.HasCheckConstraint("CK_Payments_CurrencyCode", "\"CurrencyCode\" BETWEEN 1 AND 999");
                table.HasCheckConstraint("CK_Payments_DifferentAccounts", "\"SenderAccountId\" <> \"ReceiverAccountId\"");
            });
        });

        modelBuilder.Entity<PaymentAttempt>(entity =>
        {
            entity.HasKey(attempt => attempt.Id);
            entity.HasIndex(attempt => new { attempt.PaymentId, attempt.AttemptNumber }).IsUnique();
            entity.Property(attempt => attempt.AttemptNumber).IsRequired();
            entity.Property(attempt => attempt.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(attempt => attempt.ExternalReference).HasMaxLength(200);
            entity.Property(attempt => attempt.ErrorCode).HasMaxLength(100);
            entity.Property(attempt => attempt.ErrorMessage).HasMaxLength(1000);
            entity.Property(attempt => attempt.StartedAtUtc).IsRequired();
            entity.HasOne(attempt => attempt.Payment)
                .WithMany(payment => payment.Attempts)
                .HasForeignKey(attempt => attempt.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("CK_PaymentAttempts_AttemptNumber_Positive", "\"AttemptNumber\" > 0"));
        });

        modelBuilder.Entity<PaymentStatusHistory>(entity =>
        {
            entity.HasKey(history => history.Id);
            entity.HasIndex(history => new { history.PaymentId, history.ChangedAtUtc });
            entity.Property(history => history.PreviousStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(history => history.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(history => history.Details).HasMaxLength(1000);
            entity.Property(history => history.ChangedAtUtc).IsRequired();
            entity.HasOne(history => history.Payment)
                .WithMany(payment => payment.StatusHistory)
                .HasForeignKey(history => history.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.HasIndex(entry => new { entry.PaymentId, entry.EntryType }).IsUnique();
            entity.Property(entry => entry.EntryType).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(entry => entry.Amount).HasPrecision(18, 2);
            entity.Property(entry => entry.CreatedAtUtc).IsRequired();
            entity.HasOne(entry => entry.Payment)
                .WithMany(payment => payment.LedgerEntries)
                .HasForeignKey(entry => entry.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(entry => entry.Account)
                .WithMany(account => account.LedgerEntries)
                .HasForeignKey(entry => entry.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("CK_LedgerEntries_Amount_Positive", "\"Amount\" > 0"));
        });
    }
}
