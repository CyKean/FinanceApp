namespace FinanceApp.UnitTests;

using System;
using System.Threading.Tasks;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
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
using Xunit;

/// <summary>
/// Two DbContexts write the same rows, and that is the normal shape of this app:
/// a long-lived one the UI reads through, and a short-lived one the background
/// sync pushes from.
/// <para>
/// The sync engine marks a row synced after every push, which bumps its version.
/// When that column was an EF concurrency token, the version each context had
/// loaded went into every UPDATE's WHERE clause, so a sync silently invalidated
/// the other context's copy and its next write failed with "the database
/// operation was expected to affect 1 row(s), but actually affected 0 row(s)" -
/// raised at the user as a sync error, on an ordinary expense.
/// </para>
/// </summary>
public class ConcurrentWriteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public ConcurrentWriteTests()
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

        _provider = services.BuildServiceProvider();
    }

    private FinanceAppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<FinanceAppDbContext>().UseSqlite(_connection).Options);

    private AccountBalanceService NewBalanceService(FinanceAppDbContext context, IAccountRepository accounts) =>
        new(
            new UnitOfWork(
                context,
                accounts,
                new CategoryRepository(context),
                new TransactionRepository(context),
                new BudgetRepository(context),
                new RecurringTransactionRepository(context),
                new FinancialGoalRepository(context),
                new SyncOperationRepository(context)),
            accounts,
            new TransactionRepository(context),
            NullLogger<AccountBalanceService>.Instance);

    private async Task<Guid> SeedAsync()
    {
        using var context = NewContext();
        var accounts = new AccountRepository(context);
        var account = new Account("Cash", AccountType.Cash, new Money(200), UserId);
        await accounts.AddAsync(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    /// <summary>What SyncService.SyncAccountAsync does once a push succeeds.</summary>
    private async Task SimulateASuccessfulPushAsync(Guid accountId)
    {
        using var context = NewContext();
        var accounts = new AccountRepository(context);
        var account = await accounts.GetByIdIncludingDeletedAsync(accountId);
        account!.MarkAsSynced();
        await accounts.UpdateAsync(account);
        await context.SaveChangesAsync();
    }

    private async Task<decimal> BalanceInDatabaseAsync()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT \"Balance\" FROM \"Accounts\"";
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ASaveSucceeds_AfterAnotherContextSyncedTheSameRow()
    {
        var accountId = await SeedAsync();

        // The UI's context holds the account it loaded for display.
        using var ui = NewContext();
        var uiAccounts = new AccountRepository(ui);
        await uiAccounts.GetByIdAsync(accountId);

        // The background sync's context pushes and bumps the row's version.
        await SimulateASuccessfulPushAsync(accountId);

        // The user then records an expense, which recalculates the balance and
        // saves through that same, now-stale, context.
        await NewBalanceService(ui, uiAccounts).RecalculateAsync(accountId);

        Assert.Equal(200m, await BalanceInDatabaseAsync());
    }

    [Fact]
    public async Task RepeatedSavesFromOneContext_AllReachTheDatabase()
    {
        var accountId = await SeedAsync();
        using var ui = NewContext();
        var accounts = new AccountRepository(ui);
        await accounts.GetByIdAsync(accountId);

        // Each recalculation bumps the version again, so a token carried in the
        // WHERE clause would fail every save after the first.
        for (var i = 1; i <= 3; i++)
        {
            await NewBalanceService(ui, accounts).RecalculateAsync(accountId);
        }

        Assert.Equal(200m, await BalanceInDatabaseAsync());
    }

    [Fact]
    public async Task TransactionsAlsoSurviveAnotherContextsSync()
    {
        var accountId = await SeedAsync();

        using (var seed = NewContext())
        {
            var categories = new CategoryRepository(seed);
            var food = new Category("Food", CategoryType.Expense, UserId);
            await categories.AddAsync(food);
            var transactions = new TransactionRepository(seed);
            await transactions.AddAsync(new Transaction(
                TransactionType.Expense, new Money(100), DateTime.Today,
                new AccountId(accountId), (CategoryId)food.Id, UserId));
            await seed.SaveChangesAsync();
        }

        using var ui = NewContext();
        var transactionsInUi = new TransactionRepository(ui);
        await transactionsInUi.GetByUserIdAsync(UserId);

        await SimulateASuccessfulPushAsync(accountId);

        // A pull adopts server state onto tracked rows and then saves.
        using (var pull = NewContext())
        {
            var accounts = new AccountRepository(pull);
            var account = await accounts.GetByIdIncludingDeletedAsync(accountId);
            account!.AdoptRemoteState(account.Id, account.CreatedAt, DateTime.UtcNow.AddMinutes(1), 99, false);
            await accounts.UpdateAsync(account);
            await pull.SaveChangesAsync();
        }

        await NewBalanceService(ui, new AccountRepository(ui)).RecalculateAsync(accountId);

        Assert.Equal(100m, await BalanceInDatabaseAsync());
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}