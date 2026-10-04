namespace FinanceApp.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Application.Validators;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

/// <summary>
/// The bug this covers: recording an expense moved the account balance, then a
/// sync put it back to the amount the account was opened with while the expense
/// still sat in the transaction list.
/// <para>
/// The cause was on the push side. A balance change was a number nudged in place,
/// and the nudge was never queued for sync - once an account had been synced
/// successfully, the outbox skipped the row as bookkeeping and the server kept
/// the pre-expense figure, which the next pull then restored.
/// </para>
/// <para>
/// A fake stands in for the Supabase boundary so the real repositories, the real
/// outbox and the real sync engine all run. The server stores what it is pushed,
/// so a test can assert on what the cloud actually ended up holding.
/// </para>
/// </summary>
public class AccountBalanceSyncTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly FakeSupabase _supabase;

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private Account _cash = default!;
    private Guid _foodId;
    private Guid _salaryId;

    public AccountBalanceSyncTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_connection);
        services.AddDbContext<FinanceAppDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IFinancialGoalRepository, FinancialGoalRepository>();
        services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();
        services.AddScoped<ISyncOperationRepository, SyncOperationRepository>();
        services.AddScoped<IAccountBalanceService, AccountBalanceService>();
        services.AddScoped<ISupabaseSyncService>(sp => new FakeSupabase(
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<IAccountBalanceService>()));

        var connectivity = new Mock<IConnectivityService>();
        connectivity.Setup(x => x.CheckConnectivityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(NetworkAccess.Internet);

        services.AddSingleton(connectivity.Object);

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _supabase = (FakeSupabase)_scope.ServiceProvider.GetRequiredService<ISupabaseSyncService>();

        SeedAsync().GetAwaiter().GetResult();
    }

    private T Resolve<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    private TransactionService BuildTransactionService()
    {
        var sp = _scope.ServiceProvider;
        return new TransactionService(
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<ITransactionRepository>(),
            sp.GetRequiredService<IAccountRepository>(),
            sp.GetRequiredService<ICategoryRepository>(),
            Mock.Of<IBudgetService>(),
            sp.GetRequiredService<IAccountBalanceService>(),
            new CreateTransactionDtoValidator(),
            new UpdateTransactionDtoValidator(),
            new TransactionFilterDtoValidator(),
            NullLogger<TransactionService>.Instance);
    }

    private SyncService BuildSyncService()
    {
        var sp = _scope.ServiceProvider;
        return new SyncService(
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<ISyncOperationRepository>(),
            sp.GetRequiredService<ITransactionRepository>(),
            sp.GetRequiredService<IAccountRepository>(),
            sp.GetRequiredService<ICategoryRepository>(),
            sp.GetRequiredService<IBudgetRepository>(),
            sp.GetRequiredService<IRecurringTransactionRepository>(),
            sp.GetRequiredService<IFinancialGoalRepository>(),
            sp.GetRequiredService<ISupabaseSyncService>(),
            sp.GetRequiredService<IConnectivityService>(),
            NullLogger<SyncService>.Instance);
    }

    private async Task SeedAsync()
    {
        var context = Resolve<FinanceAppDbContext>();

        // The report's account: 200.00 PHP, opened and made the default.
        _cash = new Account("Cash", AccountType.Cash, new Money(200), UserId, isDefault: true);
        context.Accounts.Add(_cash);

        var food = new Category("Food", CategoryType.Expense, UserId);
        var salary = new Category("Salary", CategoryType.Income, UserId);
        context.Categories.AddRange(food, salary);

        await context.SaveChangesAsync();

        _foodId = food.Id;
        _salaryId = salary.Id;
    }

    private Task<TransactionDto> RecordAsync(TransactionType type, decimal amount) =>
        BuildTransactionService().CreateAsync(
            new CreateTransactionDto(
                type,
                new Money(amount),
                DateTime.Today,
                (AccountId)_cash.Id,
                (CategoryId)(type == TransactionType.Expense ? _foodId : _salaryId)),
            UserId);

    private async Task<decimal> BalanceOfAsync()
    {
        // A fresh context, as after an app restart: the number the user sees has
        // to have reached the database, not just an object in memory.
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        return (await repository.GetByIdAsync(_cash.Id))!.Balance.Amount;
    }

    [Fact]
    public async Task ABalanceChange_IsQueuedForSync_WhenAnExpenseIsRecorded()
    {
        await RecordAsync(TransactionType.Expense, 100);

        // The defect in one assertion: the outbox used to end up with nothing for
        // the account, so the reduced balance existed only on this device.
        var context = Resolve<FinanceAppDbContext>();
        var pending = await context.SyncOperations
            .Where(o => o.EntityType == "Account" && o.Status != SyncStatus.Synced)
            .CountAsync();

        Assert.True(pending > 0, "The balance change did not queue an account operation for sync");
    }

    [Fact]
    public async Task ABalanceChange_IsQueuedForSync_AfterTheAccountHasAlreadyBeenSynced()
    {
        // The order the bug needs: the account syncs successfully first, so the
        // outbox already holds a synced operation for it. A change made after
        // that used to be discarded as bookkeeping rather than queued, and the
        // server was left holding the balance from before the expense.
        await BuildSyncService().SyncAsync(UserId);
        Assert.Equal(200m, _supabase.Accounts[_cash.Id].Balance);

        await RecordAsync(TransactionType.Expense, 100);

        var context = Resolve<FinanceAppDbContext>();
        var pending = await context.SyncOperations
            .Where(o => o.EntityType == "Account" && o.Status != SyncStatus.Synced)
            .CountAsync();

        Assert.True(pending > 0,
            "The balance change did not queue an account operation for sync, so the server keeps the old balance");

        var result = await BuildSyncService().SyncAsync(UserId);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(100m, _supabase.Accounts[_cash.Id].Balance);
        Assert.Equal(100m, await BalanceOfAsync());
    }

    [Fact]
    public async Task Expense_ReachesTheServer_WhenSynced()
    {
        await RecordAsync(TransactionType.Expense, 100);

        var result = await BuildSyncService().SyncAsync(UserId);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(100m, _supabase.Accounts[_cash.Id].Balance);
    }

    [Fact]
    public async Task Expense_StaysCounted_AfterTheSyncThatRunsNext()
    {
        await RecordAsync(TransactionType.Expense, 100);

        await BuildSyncService().SyncAsync(UserId);
        var afterFirstSync = await BalanceOfAsync();

        await BuildSyncService().SyncAsync(UserId);

        Assert.Equal(100m, afterFirstSync);
        Assert.Equal(100m, await BalanceOfAsync());
        Assert.Equal(100m, _supabase.Accounts[_cash.Id].Balance);
    }

    [Fact]
    public async Task Income_ReachesTheServer_AndStays()
    {
        await RecordAsync(TransactionType.Expense, 100);
        await BuildSyncService().SyncAsync(UserId);

        await RecordAsync(TransactionType.Income, 50);
        await BuildSyncService().SyncAsync(UserId);

        Assert.Equal(150m, await BalanceOfAsync());
        Assert.Equal(150m, _supabase.Accounts[_cash.Id].Balance);
    }

    [Fact]
    public async Task AnExpenseRecordedOffline_IsApplied_WhenItIsSyncedLater()
    {
        // Nothing reaches the server while offline, so the balance is only ever
        // known locally until connectivity returns.
        var offline = new Mock<IConnectivityService>();
        offline.Setup(x => x.CheckConnectivityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(NetworkAccess.None);

        var sp = _scope.ServiceProvider;
        var offlineSync = new SyncService(
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<ISyncOperationRepository>(),
            sp.GetRequiredService<ITransactionRepository>(),
            sp.GetRequiredService<IAccountRepository>(),
            sp.GetRequiredService<ICategoryRepository>(),
            sp.GetRequiredService<IBudgetRepository>(),
            sp.GetRequiredService<IRecurringTransactionRepository>(),
            sp.GetRequiredService<IFinancialGoalRepository>(),
            sp.GetRequiredService<ISupabaseSyncService>(),
            offline.Object,
            NullLogger<SyncService>.Instance);

        await RecordAsync(TransactionType.Expense, 100);
        Assert.False((await offlineSync.SyncAsync(UserId)).Success);
        Assert.Equal(100m, await BalanceOfAsync());

        var online = await BuildSyncService().SyncAsync(UserId);

        Assert.True(online.Success, online.ErrorMessage);
        Assert.Equal(100m, await BalanceOfAsync());
        Assert.Equal(100m, _supabase.Accounts[_cash.Id].Balance);
    }

    [Fact]
    public async Task APullWithAStaleServerBalance_StillDerivesTheRightBalance()
    {
        await RecordAsync(TransactionType.Expense, 100);
        await BuildSyncService().SyncAsync(UserId);

        // Reproduce what the server was left holding when the balance change was
        // dropped instead of pushed: the pre-expense figure, with a newer
        // timestamp so a last-write-wins merge would accept it.
        var stale = _supabase.Accounts[_cash.Id];
        _supabase.Accounts[_cash.Id] = stale with { Balance = 200m, UpdatedAt = DateTime.UtcNow.AddMinutes(1) };

        await Resolve<ISupabaseSyncService>().PullAsync(UserId);

        Assert.Equal(100m, await BalanceOfAsync());
    }

    [Fact]
    public async Task TotalBalance_SurvivesSync_AndMatchesTheTransactions()
    {
        var context = Resolve<FinanceAppDbContext>();
        context.Accounts.Add(new Account("Maribank", AccountType.Bank, new Money(3346), UserId));
        await context.SaveChangesAsync();

        await RecordAsync(TransactionType.Expense, 100);
        await BuildSyncService().SyncAsync(UserId);

        var total = await Resolve<IAccountRepository>().GetTotalBalanceAsync(UserId);

        // Cash 100 + Maribank 3,346, against the 3,654 the balance column alone
        // would show if the expense were ignored.
        Assert.Equal(3446m, total.Amount);
    }

    [Fact]
    public async Task Transactions_KeepTheirAccountLink_ThroughSync()
    {
        var expense = await RecordAsync(TransactionType.Expense, 100);

        await BuildSyncService().SyncAsync(UserId);

        var pushed = _supabase.Transactions[expense.Id];
        Assert.Equal(_cash.Id, pushed.AccountId);

        // And the link is intact locally, which is what makes the expense count
        // against Cash when the balance is derived.
        var stored = await Resolve<ITransactionRepository>().GetByIdAsync(expense.Id);
        Assert.Equal((AccountId)_cash.Id, stored!.AccountId);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record ServerAccount(
        Guid Id,
        DateTime CreatedAt,
        Guid UserId,
        string Name,
        string Type,
        decimal Balance,
        string Currency,
        decimal InitialBalanceAmount,
        bool IsDefault,
        bool IsDeleted,
        DateTime UpdatedAt,
        int Version);

    private sealed record ServerTransaction(
        Guid Id,
        DateTime CreatedAt,
        Guid UserId,
        TransactionType Type,
        decimal Amount,
        string Currency,
        DateTime Date,
        Guid AccountId,
        Guid CategoryId,
        bool IsDeleted,
        DateTime UpdatedAt,
        int Version);

    /// <summary>
    /// Stands in for Supabase: keeps the rows it is pushed and merges them back
    /// the way the real transport does, including the recalculation that runs
    /// once a pull has finished.
    /// </summary>
    private sealed class FakeSupabase : ISupabaseSyncService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAccountBalanceService _accountBalanceService;

        public Dictionary<Guid, ServerAccount> Accounts { get; } = new();
        public Dictionary<Guid, ServerTransaction> Transactions { get; } = new();

        public FakeSupabase(IUnitOfWork unitOfWork, IAccountBalanceService accountBalanceService)
        {
            _unitOfWork = unitOfWork;
            _accountBalanceService = accountBalanceService;
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SyncAccountAsync(Account entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
        {
            if (operationType == SyncOperationType.Delete)
            {
                Accounts.Remove(entity.Id);
                return Task.CompletedTask;
            }

            Accounts[entity.Id] = new ServerAccount(
                entity.Id, entity.CreatedAt, entity.UserId, entity.Name, entity.Type.ToString(),
                entity.Balance.Amount, entity.Balance.Currency, entity.InitialBalanceAmount,
                entity.IsDefault, entity.IsDeleted, entity.UpdatedAt, entity.Version);
            return Task.CompletedTask;
        }

        public Task SyncTransactionAsync(Transaction entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
        {
            if (operationType == SyncOperationType.Delete)
            {
                Transactions.Remove(entity.Id);
                return Task.CompletedTask;
            }

            Transactions[entity.Id] = new ServerTransaction(
                entity.Id, entity.CreatedAt, entity.UserId, entity.Type,
                entity.Amount.Amount, entity.Amount.Currency, entity.Date,
                entity.AccountId.Value, entity.CategoryId.Value,
                entity.IsDeleted, entity.UpdatedAt, entity.Version);
            return Task.CompletedTask;
        }

        public Task SyncCategoryAsync(Category entity, SyncOperationType operationType, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SyncBudgetAsync(Budget entity, SyncOperationType operationType, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SyncRecurringTransactionAsync(RecurringTransaction entity, SyncOperationType operationType, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SyncFinancialGoalAsync(FinancialGoal entity, SyncOperationType operationType, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SyncSyncOperationAsync(SyncOperation entity, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task<int> PullAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var merged = 0;

            foreach (var row in Accounts.Values.Where(a => a.UserId == userId))
            {
                var local = await _unitOfWork.Accounts.GetByIdAsync(row.Id, cancellationToken);
                if (local == null)
                {
                    // The opening balance is what a new device needs; starting
                    // from the cached balance would count the transactions that
                    // are about to arrive a second time.
                    var created = new Account(
                        row.Name,
                        Enum.Parse<AccountType>(row.Type),
                        new Money(row.InitialBalanceAmount, row.Currency),
                        row.UserId,
                        isDefault: row.IsDefault);

                    await _unitOfWork.Accounts.AddAsync(created, cancellationToken);
                    created.AdoptRemoteState(row.Id, row.CreatedAt, row.UpdatedAt, row.Version, row.IsDeleted);
                    merged++;
                }
                else if (!IsPending(local.SyncStatus) && row.UpdatedAt > local.UpdatedAt)
                {
                    local.UpdateName(row.Name);
                    // The balance is deliberately left alone, as the real pull
                    // does: it is derived from the transactions merged below.
                    local.AdoptRemoteState(row.Id, row.CreatedAt, row.UpdatedAt, row.Version, row.IsDeleted);
                    await _unitOfWork.Accounts.UpdateAsync(local, cancellationToken);
                    merged++;
                }
            }

            foreach (var row in Transactions.Values.Where(t => t.UserId == userId))
            {
                var local = await _unitOfWork.Transactions.GetByIdAsync(row.Id, cancellationToken);
                if (local == null)
                {
                    var created = new Transaction(
                        row.Type,
                        new Money(row.Amount, row.Currency),
                        row.Date,
                        new AccountId(row.AccountId),
                        new CategoryId(row.CategoryId),
                        row.UserId);

                    await _unitOfWork.Transactions.AddAsync(created, cancellationToken);
                    created.AdoptRemoteState(row.Id, row.CreatedAt, row.UpdatedAt, row.Version, row.IsDeleted);
                    merged++;
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _accountBalanceService.RecalculateAllAsync(userId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return merged;
        }

        private static bool IsPending(SyncStatus status) =>
            status is SyncStatus.PendingCreate or SyncStatus.PendingUpdate or SyncStatus.PendingDelete;
    }
}