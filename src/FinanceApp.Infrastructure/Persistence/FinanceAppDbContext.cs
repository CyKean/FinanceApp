namespace FinanceApp.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Domain.Common;

public class FinanceAppDbContext : DbContext
{
    public FinanceAppDbContext(DbContextOptions<FinanceAppDbContext> options)
        : base(options)
    {
        // Offline-first guarantee: hosted-service startup is not a reliable place
        // to create the schema on every platform, so ensure it before first use.
        // Idempotent - a no-op when tables already exist.
        //
        // Deliberately not memoised behind a "once per process" flag. This is a
        // metadata probe costing microseconds, and a process-wide guard silently
        // skips schema creation for any second database in the same process -
        // which is exactly how "no such table" regressions happen.
        Database.EnsureCreated();
        // Adds tables/columns introduced after v1 (EnsureCreated never alters an
        // existing database).
        DatabaseInitializer.EnsureExtraColumns(this);
        DatabaseInitializer.EnsureLocalUsersTable(this);
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<FinancialGoal> FinancialGoals => Set<FinancialGoal>();
    public DbSet<SyncOperation> SyncOperations => Set<SyncOperation>();
    public DbSet<LocalUser> LocalUsers => Set<LocalUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Device-local credentials. No soft-delete filter: a login lookup must
        // still resolve an account regardless of anything else.
        modelBuilder.Entity<LocalUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(320);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.LastLoginAt);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).HasConversion<int>().IsRequired();
            entity.OwnsOne(e => e.Balance, b =>
            {
                b.Property(m => m.Amount).HasColumnName("Balance").HasColumnType("decimal(18,2)").IsRequired();
                b.Property(m => m.Currency).HasColumnName("BalanceCurrency").HasMaxLength(3).IsRequired().HasDefaultValue("PHP");
            });
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.Color).HasMaxLength(7);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.IsDefault).HasDefaultValue(false);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsDefault });
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).HasConversion<int>().IsRequired();
            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.Color).HasMaxLength(7);
            entity.Property(e => e.ParentCategoryId);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.IsSystem).HasDefaultValue(false);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.Type });
            entity.HasIndex(e => new { e.UserId, e.Type, e.IsActive });
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Type).HasConversion<int>().IsRequired();
            entity.OwnsOne(e => e.Amount, b =>
            {
                b.Property(m => m.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
                b.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3).IsRequired().HasDefaultValue("PHP");
            });
            entity.Property(e => e.Date).HasColumnType("date").IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.AccountId).HasConversion(v => v.Value, v => v == Guid.Empty ? default : new AccountId(v)).IsRequired();
            entity.Property(e => e.CategoryId).HasConversion(v => v.Value, v => v == Guid.Empty ? default : new CategoryId(v)).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.RecurringTransactionId);
            entity.Property(e => e.SyncStatus).HasConversion<int>().HasDefaultValue(SyncStatus.PendingCreate);
            entity.Property(e => e.LastSyncedAt);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.Date });
            entity.HasIndex(e => new { e.UserId, e.Type });
            entity.HasIndex(e => new { e.UserId, e.AccountId });
            entity.HasIndex(e => new { e.UserId, e.CategoryId });
            entity.HasIndex(e => new { e.UserId, e.SyncStatus });
            entity.HasIndex(e => e.RecurringTransactionId);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Budget>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.OwnsOne(e => e.Amount, b =>
            {
                b.Property(m => m.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
                b.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3).IsRequired().HasDefaultValue("PHP");
            });
            entity.OwnsOne(e => e.SpentAmount, b =>
            {
                b.Property(m => m.Amount).HasColumnName("SpentAmount").HasColumnType("decimal(18,2)").HasDefaultValue(0);
                b.Property(m => m.Currency).HasColumnName("SpentAmountCurrency").HasMaxLength(3).IsRequired().HasDefaultValue("PHP");
            });
            entity.Property(e => e.StartDate).HasColumnType("date").IsRequired();
            entity.Property(e => e.EndDate).HasColumnType("date").IsRequired();
            entity.Property(e => e.CategoryId).HasConversion(v => v.Value, v => v == Guid.Empty ? default : new CategoryId(v)).IsRequired();
            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.Color).HasMaxLength(7);
            entity.Property(e => e.LinkedAccountId).HasConversion(v => v == null ? (Guid?)null : v.Value.Value, v => v == null || v.Value == Guid.Empty ? (AccountId?)null : new AccountId(v.Value));
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.SyncStatus).HasConversion<int>().HasDefaultValue(SyncStatus.PendingCreate);
            entity.Property(e => e.LastSyncedAt);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.StartDate, e.EndDate });
            entity.HasIndex(e => new { e.UserId, e.CategoryId });
            entity.HasIndex(e => new { e.UserId, e.SyncStatus });
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<RecurringTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).HasConversion<int>().IsRequired();
            entity.OwnsOne(e => e.Amount, b =>
            {
                b.Property(m => m.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
                b.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3).IsRequired().HasDefaultValue("PHP");
            });
            entity.Property(e => e.Frequency).HasConversion<int>().IsRequired();
            entity.Property(e => e.StartDate).HasColumnType("date").IsRequired();
            entity.Property(e => e.EndDate).HasColumnType("date");
            entity.Property(e => e.AccountId).HasConversion(v => v.Value, v => v == Guid.Empty ? default : new AccountId(v)).IsRequired();
            entity.Property(e => e.CategoryId).HasConversion(v => v.Value, v => v == Guid.Empty ? default : new CategoryId(v)).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.LastGeneratedAt);
            entity.Property(e => e.NextDueDate).HasColumnType("date");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SyncStatus).HasConversion<int>().HasDefaultValue(SyncStatus.PendingCreate);
            entity.Property(e => e.LastSyncedAt);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsActive });
            entity.HasIndex(e => e.NextDueDate);
            entity.HasIndex(e => new { e.UserId, e.SyncStatus });
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<FinancialGoal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.OwnsOne(e => e.TargetAmount, b =>
            {
                b.Property(m => m.Amount).HasColumnName("TargetAmount").HasColumnType("decimal(18,2)").IsRequired();
                b.Property(m => m.Currency).HasColumnName("TargetAmountCurrency").HasMaxLength(3).IsRequired().HasDefaultValue("PHP");
            });
            entity.OwnsOne(e => e.CurrentAmount, b =>
            {
                b.Property(m => m.Amount).HasColumnName("CurrentAmount").HasColumnType("decimal(18,2)").HasDefaultValue(0);
                b.Property(m => m.Currency).HasColumnName("CurrentAmountCurrency").HasMaxLength(3).IsRequired().HasDefaultValue("PHP");
            });
            entity.Property(e => e.TargetDate).HasColumnType("date").IsRequired();
            entity.Property(e => e.StartDate).HasColumnType("date").IsRequired();
            entity.Property(e => e.Status).HasConversion<int>().HasDefaultValue(GoalStatus.Active);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.Color).HasMaxLength(7);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.LinkedAccountId).HasConversion(v => v == null ? (Guid?)null : v.Value.Value, v => v == null || v.Value == Guid.Empty ? (AccountId?)null : new AccountId(v.Value));
            entity.Property(e => e.SyncStatus).HasConversion<int>().HasDefaultValue(SyncStatus.PendingCreate);
            entity.Property(e => e.LastSyncedAt);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => new { e.UserId, e.SyncStatus });
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<SyncOperation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EntityId).IsRequired();
            entity.Property(e => e.OperationType).HasConversion<int>().IsRequired();
            entity.Property(e => e.Status).HasConversion<int>().HasDefaultValue(SyncStatus.PendingCreate);
            entity.Property(e => e.Payload).HasColumnType("TEXT");
            entity.Property(e => e.RetryCount).HasDefaultValue(0);
            entity.Property(e => e.LastAttemptAt);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.Version).IsRequired().IsConcurrencyToken();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasQueryFilter(e => !e.IsDeleted);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is Entity && (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            if (entry.Entity is Entity entity)
            {
                if (entry.State == EntityState.Added)
                {
                    entity.UpdateTimestamp();
                }
                else if (entry.State == EntityState.Modified)
                {
                    entity.UpdateTimestamp();
                }
            }
        }

        if (EnqueueSyncOperations() > 0)
            FinanceApp.Application.SyncNotifications.RaiseDataChanged();

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Offline-first outbox: every user-driven insert/update/delete queues a
    /// <see cref="SyncOperation"/> in the same transaction so the background
    /// sync can push it when online. Sync bookkeeping itself (Synced/Failed
    /// rows) and SyncOperation rows never enqueue. Returns queued count.
    /// </summary>
    private int EnqueueSyncOperations()
    {
        var queued = 0;
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is not Entity entity)
                continue;

            if (entry.Entity is SyncOperation)
                continue;

            SyncOperationType operationType;
            if (entry.State == EntityState.Deleted)
            {
                operationType = SyncOperationType.Delete;
            }
            else if (entry.State == EntityState.Added)
            {
                if (entity.SyncStatus == SyncStatus.Synced)
                    continue; // Pull-adopted rows arrive already synced.

                operationType = SyncOperationType.Create;
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entity.SyncStatus == SyncStatus.PendingDelete)
                    operationType = SyncOperationType.Delete;
                else if (entity.SyncStatus == SyncStatus.PendingCreate ||
                         entity.SyncStatus == SyncStatus.PendingUpdate)
                    operationType = SyncOperationType.Update;
                else
                    continue; // Synced/Failed bookkeeping - not user changes.
            }
            else
            {
                continue;
            }

            var userId = GetEntityUserId(entity);
            if (userId == Guid.Empty)
                continue;

            Set<SyncOperation>().Add(new SyncOperation(
                entity.GetType().Name,
                entity.Id,
                operationType,
                userId));
            queued++;
        }

        return queued;
    }

    private static Guid GetEntityUserId(Entity entity) => entity switch
    {
        Account a => a.UserId,
        Category c => c.UserId,
        Transaction t => t.UserId,
        Budget b => b.UserId,
        RecurringTransaction r => r.UserId,
        FinancialGoal g => g.UserId,
        _ => Guid.Empty
    };
}