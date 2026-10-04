namespace FinanceApp.UnitTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Application.Validators;
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
/// An account's balance is derived: the balance it was opened with, plus its
/// income, minus its expenses.
/// <para>
/// It used to be a stored number that each transaction nudged by hand. That let
/// the stored figure and the transaction list disagree - the nudge could be lost,
/// and a pull that trusted the stored column put the server's older number back,
/// so a recorded expense silently stopped affecting the balance. Deriving it means
/// there is nothing to keep in step.
/// </para>
/// <para>
/// These run against a real SQLite database because the derivation reads an
/// aggregate over value-converted and owned properties, which only fails at
/// translation time - a mocked repository would never notice.
/// </para>
/// </summary>
public class AccountBalanceServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private Account _cash = default!;
    private Account _bank = default!;
    private Guid _foodId;
    private Guid _salaryId;

    public AccountBalanceServiceTests()
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
        _scope = _provider.CreateScope();

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

    private async Task SeedAsync()
    {
        var context = Resolve<FinanceAppDbContext>();
        var accounts = Resolve<IAccountRepository>();

        _cash = new Account("Cash", AccountType.Cash, new Money(200), UserId, isDefault: true);
        _bank = new Account("Maribank", AccountType.Bank, new Money(3346), UserId);
        await accounts.AddAsync(_cash);
        await accounts.AddAsync(_bank);

        var food = new Category("Food", CategoryType.Expense, UserId);
        var salary = new Category("Salary", CategoryType.Income, UserId);
        context.Categories.AddRange(food, salary);

        await context.SaveChangesAsync();

        _foodId = food.Id;
        _salaryId = salary.Id;
    }

    private Task<TransactionDto> RecordAsync(TransactionType type, decimal amount, Account account, Guid? categoryId = null) =>
        BuildTransactionService().CreateAsync(
            new CreateTransactionDto(
                type,
                new Money(amount),
                DateTime.Today,
                (AccountId)account.Id,
                (CategoryId)(categoryId ?? (type == TransactionType.Expense ? _foodId : _salaryId))),
            UserId);

    private async Task<decimal> BalanceOfAsync(Guid accountId)
    {
        // Read through a fresh context: the account the tests hold is the one the
        // service just mutated, and asserting on that would not prove the balance
        // reached the database - which is the whole point after a restart.
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var stored = await repository.GetByIdAsync(accountId);
        return stored!.Balance.Amount;
    }

    [Fact]
    public async Task Expense_ReducesTheSelectedAccountsBalance()
    {
        await RecordAsync(TransactionType.Expense, 100, _cash);

        Assert.Equal(100m, await BalanceOfAsync(_cash.Id));
        // The other account's balance is none of this expense's business.
        Assert.Equal(3346m, await BalanceOfAsync(_bank.Id));
    }

    [Fact]
    public async Task Income_IncreasesTheSelectedAccountsBalance()
    {
        await RecordAsync(TransactionType.Income, 500, _bank);

        Assert.Equal(3846m, await BalanceOfAsync(_bank.Id));
        Assert.Equal(200m, await BalanceOfAsync(_cash.Id));
    }

    [Fact]
    public async Task Balance_AddsUpIncomeAndExpensesAcrossEveryTransaction()
    {
        await RecordAsync(TransactionType.Expense, 100, _cash);
        await RecordAsync(TransactionType.Expense, 25, _cash);
        await RecordAsync(TransactionType.Income, 1000, _cash);
        await RecordAsync(TransactionType.Expense, 46, _bank);

        Assert.Equal(1075m, await BalanceOfAsync(_cash.Id));
        Assert.Equal(3300m, await BalanceOfAsync(_bank.Id));
    }

    [Fact]
    public async Task TotalBalance_EqualsTheSumOfEveryAccountBalance()
    {
        await RecordAsync(TransactionType.Expense, 100, _cash);
        await RecordAsync(TransactionType.Expense, 46, _bank);

        // Cash 200 + Maribank 3,346 opening, less 100 and 46 spent.
        var total = await Resolve<IAccountRepository>().GetTotalBalanceAsync(UserId);

        Assert.Equal(3400m, total.Amount);
    }

    [Fact]
    public async Task EditingAnAmount_MovesTheBalanceWithIt()
    {
        var expense = await RecordAsync(TransactionType.Expense, 100, _cash);

        await BuildTransactionService().UpdateAsync(
            expense.Id, new UpdateTransactionDto(Amount: new Money(250)), UserId);

        Assert.Equal(-50m, await BalanceOfAsync(_cash.Id));
    }

    [Fact]
    public async Task MovingATransaction_CreditsTheAccountItLeftAndDebitsTheOneItJoined()
    {
        var expense = await RecordAsync(TransactionType.Expense, 100, _cash);

        await BuildTransactionService().UpdateAsync(
            expense.Id, new UpdateTransactionDto(AccountId: (AccountId)_bank.Id), UserId);

        // Re-classifying spending must not invent or destroy money: Cash goes back
        // to its opening balance and Maribank absorbs the expense.
        Assert.Equal(200m, await BalanceOfAsync(_cash.Id));
        Assert.Equal(3246m, await BalanceOfAsync(_bank.Id));
    }

    [Fact]
    public async Task DeletingAnExpense_HandsTheBalanceBack()
    {
        var expense = await RecordAsync(TransactionType.Expense, 100, _cash);

        await BuildTransactionService().DeleteAsync(expense.Id, UserId);

        Assert.Equal(200m, await BalanceOfAsync(_cash.Id));
    }

    [Fact]
    public async Task DeletedTransactions_StopCountingTowardTheBalance()
    {
        var expense = await RecordAsync(TransactionType.Expense, 100, _cash);
        var context = Resolve<FinanceAppDbContext>();

        // Marked deleted rather than removed: the row stays, the query filter
        // stops seeing it, and the balance has to follow.
        var transaction = await context.Transactions.SingleAsync(t => t.Id == expense.Id);
        transaction.MarkAsDeleted();
        await context.SaveChangesAsync();

        await Resolve<IAccountBalanceService>().RecalculateAsync(_cash.Id);

        Assert.Equal(200m, await BalanceOfAsync(_cash.Id));
    }

    [Fact]
    public async Task Recalculation_RepairsABalanceThatWasOverwrittenWithAStaleFigure()
    {
        await RecordAsync(TransactionType.Expense, 100, _cash);
        Assert.Equal(100m, await BalanceOfAsync(_cash.Id));

        // What a pull that trusted the server's cached column used to do.
        var context = Resolve<FinanceAppDbContext>();
        var account = await context.Accounts.SingleAsync(a => a.Id == _cash.Id);
        account.SetBalance(new Money(200));
        await context.SaveChangesAsync();
        Assert.Equal(200m, await BalanceOfAsync(_cash.Id));

        await Resolve<IAccountBalanceService>().RecalculateAllAsync(UserId);

        Assert.Equal(100m, await BalanceOfAsync(_cash.Id));
    }

    [Fact]
    public async Task BalanceIsRebuiltFromTransactions_OnAFreshContext()
    {
        await RecordAsync(TransactionType.Expense, 100, _cash);

        // A new DbContext, as after an app restart or a sign-in on another
        // device: the opening balance and the transactions are all it has.
        var derived = await BalanceOfAsync(_cash.Id);

        Assert.Equal(100m, derived);
    }

    [Fact]
    public async Task AnotherUsersTransactions_DoNotAffectTheBalance()
    {
        var otherUser = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var context = Resolve<FinanceAppDbContext>();

        var otherCash = new Account("Other Cash", AccountType.Cash, new Money(500), otherUser);
        await context.Accounts.AddAsync(otherCash);
        var otherFood = new Category("Other Food", CategoryType.Expense, otherUser);
        await context.Categories.AddAsync(otherFood);
        await context.SaveChangesAsync();

        await Resolve<ITransactionRepository>().AddAsync(
            new Transaction(
                TransactionType.Expense, new Money(400), DateTime.Today,
                (AccountId)otherCash.Id, (CategoryId)otherFood.Id, otherUser));
        await context.SaveChangesAsync();

        await Resolve<IAccountBalanceService>().RecalculateAllAsync(UserId);

        // This user's spending is none of the other user's business, and a
        // recalculation scoped to one user must not reach into another's.
        Assert.Equal(200m, await BalanceOfAsync(_cash.Id));
        Assert.Equal(500m, await BalanceOfAsync(otherCash.Id));

        await Resolve<IAccountBalanceService>().RecalculateAllAsync(otherUser);

        Assert.Equal(100m, await BalanceOfAsync(otherCash.Id));
        Assert.Equal(200m, await BalanceOfAsync(_cash.Id));
    }

    [Fact]
    public async Task RecalculatingOneAccount_ReadsOnlyThatAccountsTransactions()
    {
        await RecordAsync(TransactionType.Expense, 100, _cash);
        await RecordAsync(TransactionType.Expense, 46, _bank);

        var net = await Resolve<ITransactionRepository>()
            .GetNetAmountsByAccountAsync(UserId, new AccountId(_cash.Id));

        // Saving one transaction must not add up the user's whole history: this
        // runs on every expense tap, over the rows of one account only.
        var cashOnly = Assert.Single(net);
        Assert.Equal(_cash.Id, cashOnly.AccountId);
        Assert.Equal(-100m, cashOnly.Amount);
    }

    [Fact]
    public async Task Recalculation_ReportsWhatItCorrected()
    {
        await RecordAsync(TransactionType.Expense, 100, _cash);
        var context = Resolve<FinanceAppDbContext>();
        var account = await context.Accounts.SingleAsync(a => a.Id == _cash.Id);
        account.SetBalance(new Money(999));
        await context.SaveChangesAsync();

        var recalculated = await Resolve<IAccountBalanceService>().RecalculateAllAsync(UserId);

        // Both accounts are visited, whether or not they needed correcting.
        Assert.Equal(2, recalculated);
        Assert.Equal(100m, await BalanceOfAsync(_cash.Id));
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}