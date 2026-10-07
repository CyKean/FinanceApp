namespace FinanceApp.UnitTests;

using System;
using System.Threading.Tasks;
using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Application.Validators;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Exceptions;
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
/// Edit-in-place semantics for transactions: identity is preserved, the row is
/// marked pending sync, derived balances/budgets refresh, and type changes
/// are validated against the target category.
/// </summary>
public class TransactionEditTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private Account _cash = default!;
    private Account _bank = default!;
    private Category _food = default!;
    private Category _transport = default!;
    private Category _salary = default!;

    public TransactionEditTests()
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

    private TransactionService BuildTransactionService(Mock<IBudgetService>? budgetService = null)
    {
        var sp = _scope.ServiceProvider;
        return new TransactionService(
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<ITransactionRepository>(),
            sp.GetRequiredService<IAccountRepository>(),
            sp.GetRequiredService<ICategoryRepository>(),
            budgetService?.Object ?? Mock.Of<IBudgetService>(),
            sp.GetRequiredService<IAccountBalanceService>(),
            new CreateTransactionDtoValidator(),
            new UpdateTransactionDtoValidator(),
            new TransactionFilterDtoValidator(),
            NullLogger<TransactionService>.Instance);
    }

    private async Task SeedAsync()
    {
        var context = Resolve<FinanceAppDbContext>();

        _cash = new Account("Cash", AccountType.Cash, new Money(500), UserId, isDefault: true);
        _bank = new Account("Bank", AccountType.Bank, new Money(1000), UserId);
        _food = new Category("Food", CategoryType.Expense, UserId);
        _transport = new Category("Transport", CategoryType.Expense, UserId);
        _salary = new Category("Salary", CategoryType.Income, UserId);
        context.Accounts.AddRange(_cash, _bank);
        context.Categories.AddRange(_food, _transport, _salary);
        await context.SaveChangesAsync();
    }

    private async Task<Transaction> CreateStoredAsync(TransactionType type, decimal amount, DateTime date, Category category, Account? account = null, string? notes = null)
    {
        var service = BuildTransactionService();
        var dto = await service.CreateAsync(
            new CreateTransactionDto(type, new Money(amount), date, (AccountId)(account ?? _cash).Id, (CategoryId)category.Id, notes),
            UserId);
        var stored = await Resolve<ITransactionRepository>().GetByIdAsync(dto.Id);
        return stored!;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Update_PreservesIdentity_BumpsVersion_MarksPending()
    {
        var txn = await CreateStoredAsync(TransactionType.Expense, 100, new DateTime(2026, 10, 4), _food, notes: "Lunch");
        var createdAt = txn.CreatedAt;
        var version = txn.Version;

        await BuildTransactionService().UpdateAsync(
            txn.Id,
            new UpdateTransactionDto(
                Amount: new Money(250),
                Date: new DateTime(2026, 10, 5),
                Notes: "Dinner",
                AccountId: (AccountId)_bank.Id,
                CategoryId: (CategoryId)_transport.Id),
            UserId);

        var updated = await Resolve<ITransactionRepository>().GetByIdAsync(txn.Id);
        Assert.NotNull(updated);
        Assert.Equal(txn.Id, updated!.Id);
        Assert.Equal(createdAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt >= txn.UpdatedAt);
        Assert.True(updated.Version > version);
        Assert.Equal(SyncStatus.PendingUpdate, updated.SyncStatus);
        Assert.Equal(250m, updated.Amount.Amount);
        Assert.Equal(new DateTime(2026, 10, 5), updated.Date);
        Assert.Equal("Dinner", updated.Notes);
        Assert.Equal(_bank.Id, updated.AccountId.Value);
        Assert.Equal(_transport.Id, updated.CategoryId.Value);
    }

    [Fact]
    public async Task Update_TypeChange_ExpenseToIncome_Persists()
    {
        var txn = await CreateStoredAsync(TransactionType.Expense, 100, DateTime.Today, _food);

        var dto = await BuildTransactionService().UpdateAsync(
            txn.Id,
            new UpdateTransactionDto(Type: TransactionType.Income, CategoryId: (CategoryId)_salary.Id),
            UserId);

        Assert.Equal(TransactionType.Income, dto.Type);
        var updated = await Resolve<ITransactionRepository>().GetByIdAsync(txn.Id);
        Assert.Equal(TransactionType.Income, updated!.Type);
        Assert.Equal(_salary.Id, updated.CategoryId.Value);
    }

    [Fact]
    public async Task Update_TypeChange_WithMismatchedCategory_ThrowsAndDoesNotPersist()
    {
        var txn = await CreateStoredAsync(TransactionType.Expense, 100, DateTime.Today, _food);
        var version = txn.Version;

        await Assert.ThrowsAsync<ValidationException>(() =>
            BuildTransactionService().UpdateAsync(
                txn.Id,
                new UpdateTransactionDto(Type: TransactionType.Income),
                UserId));

        var unchanged = await Resolve<ITransactionRepository>().GetByIdAsync(txn.Id);
        Assert.Equal(TransactionType.Expense, unchanged!.Type);
        Assert.Equal(version, unchanged.Version);
    }

    [Fact]
    public async Task Update_NotesWhitespaceOnly_BecomesNull()
    {
        var txn = await CreateStoredAsync(TransactionType.Expense, 100, DateTime.Today, _food, notes: "Lunch");

        await BuildTransactionService().UpdateAsync(
            txn.Id,
            new UpdateTransactionDto(Notes: "   "),
            UserId);

        var updated = await Resolve<ITransactionRepository>().GetByIdAsync(txn.Id);
        Assert.Null(updated!.Notes);
    }

    [Fact]
    public async Task Update_IncomeToExpense_RefreshesBudgetSpending()
    {
        var txn = await CreateStoredAsync(TransactionType.Income, 900, DateTime.Today, _salary);
        var budget = new BudgetDto(Guid.NewGuid(), "Food", new Money(500), new Money(0), new Money(500), 0m, DateTime.Today, DateTime.Today.AddMonths(1), (CategoryId)_food.Id, "Food", "", "", null, null, false, false, SyncStatus.Synced, null, DateTime.UtcNow, DateTime.UtcNow, false, null);

        var budgetService = new Mock<IBudgetService>();
        budgetService.Setup(b => b.GetActiveForCategoryAsync(UserId, It.IsAny<CategoryId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(budget);

        await BuildTransactionService(budgetService).UpdateAsync(
            txn.Id,
            new UpdateTransactionDto(Type: TransactionType.Expense, CategoryId: (CategoryId)_food.Id),
            UserId);

        budgetService.Verify(b => b.RecalculateSpentAsync(budget.Id, UserId, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Update_ExpenseTypeCaseNever_Inserts()
    {
        var txn = await CreateStoredAsync(TransactionType.Expense, 100, DateTime.Today, _food);

        await BuildTransactionService().UpdateAsync(
            txn.Id,
            new UpdateTransactionDto(Amount: new Money(42)),
            UserId);

        var context = Resolve<FinanceAppDbContext>();
        Assert.Equal(1, context.Transactions.Count(t => t.UserId == UserId));
    }
}
