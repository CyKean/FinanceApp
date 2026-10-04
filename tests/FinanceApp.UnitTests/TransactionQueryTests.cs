namespace FinanceApp.UnitTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// These queries touch value-converted (<c>CategoryId</c>) and owned
/// (<c>Amount</c>) properties, and EF only fails to translate them when they run.
/// A mock-based test would sail past that, so every one of these executes real
/// SQL against SQLite.
/// </summary>
public class TransactionQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    // One scope for the whole test: disposing it before the repository is used
    // disposes the DbContext the repository holds.
    private readonly IServiceScope _scope;

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private Guid _foodId;
    private Guid _transportId;
    private Guid _salaryId;
    private Guid _accountId;

    public TransactionQueryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_connection);
        services.AddDbContext<FinanceAppDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));
        services.AddScoped<ITransactionRepository, FinanceApp.Infrastructure.Repositories.TransactionRepository>();
        services.AddScoped<ICategoryRepository, FinanceApp.Infrastructure.Repositories.CategoryRepository>();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        SeedAsync().GetAwaiter().GetResult();
    }

    private T Resolve<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    private async Task SeedAsync()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FinanceAppDbContext>();

        var food = new Category("Food", CategoryType.Expense, UserId);
        var transport = new Category("Transport", CategoryType.Expense, UserId);
        var salary = new Category("Salary", CategoryType.Income, UserId);
        context.Categories.AddRange(food, transport, salary);

        var account = new Account("Cash", AccountType.Cash, new Money(1000), UserId);
        context.Accounts.Add(account);

        // January: 3 food expenses, 1 transport, 1 salary income.
        context.Transactions.AddRange(
            Expense(100, new DateTime(2026, 1, 5), food.Id, account.Id, UserId, "a"),
            Expense(50, new DateTime(2026, 1, 10), food.Id, account.Id, UserId, "b"),
            Expense(25, new DateTime(2026, 1, 20), food.Id, account.Id, UserId, "c"),
            Expense(70, new DateTime(2026, 1, 22), transport.Id, account.Id, UserId, "d"),
            new Transaction(TransactionType.Income, new Money(900), new DateTime(2026, 1, 25), (AccountId)account.Id, (CategoryId)salary.Id, UserId, "e"));

        // February: 1 food expense, 1 salary income.
        context.Transactions.AddRange(
            Expense(200, new DateTime(2026, 2, 3), food.Id, account.Id, UserId, "f"),
            new Transaction(TransactionType.Income, new Money(1000), new DateTime(2026, 2, 25), (AccountId)account.Id, (CategoryId)salary.Id, UserId, "g"));

        // Another user's row must never leak into the aggregates.
        context.Transactions.Add(
            Expense(9999, new DateTime(2026, 1, 15), food.Id, account.Id, OtherUserId, "other"));

        await context.SaveChangesAsync();

        _foodId = food.Id;
        _transportId = transport.Id;
        _salaryId = salary.Id;
        _accountId = account.Id;
    }

    private static Transaction Expense(decimal amount, DateTime date, Guid categoryId, Guid accountId, Guid userId, string notes) =>
        new(TransactionType.Expense, new Money(amount), date, (AccountId)accountId, (CategoryId)categoryId, userId, notes);

    [Fact]
    public async Task GetPagedAsync_ReturnsOnlyTheRequestedPage()
    {
        var repository = Resolve<ITransactionRepository>();

        var firstPage = await repository.GetPagedAsync(UserId, new TransactionQuery(), 1, 3);

        Assert.Equal(3, firstPage.Count);
        // Newest first.
        Assert.Equal(new DateTime(2026, 2, 25), firstPage[0].Date);
        Assert.Equal(new DateTime(2026, 2, 3), firstPage[1].Date);
        Assert.Equal(new DateTime(2026, 1, 25), firstPage[2].Date);
    }

    [Fact]
    public async Task GetPagedAsync_PagesDoNotOverlap()
    {
        var repository = Resolve<ITransactionRepository>();

        var page1 = await repository.GetPagedAsync(UserId, new TransactionQuery(), 1, 3);
        var page2 = await repository.GetPagedAsync(UserId, new TransactionQuery(), 2, 3);
        var page3 = await repository.GetPagedAsync(UserId, new TransactionQuery(), 3, 3);

        Assert.Equal(3, page1.Count);
        Assert.Equal(3, page2.Count);
        Assert.Single(page3);
        Assert.Empty(page1.Select(t => t.Id).Intersect(page2.Select(t => t.Id)));
        Assert.Empty(page2.Select(t => t.Id).Intersect(page3.Select(t => t.Id)));
    }

    [Fact]
    public async Task GetPagedAsync_AppliesTheDateFilterInSql()
    {
        var repository = Resolve<ITransactionRepository>();

        var january = await repository.GetPagedAsync(
            UserId,
            new TransactionQuery { StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31) },
            1,
            50);

        Assert.Equal(5, january.Count);
        Assert.All(january, t => Assert.Equal(1, t.Date.Month));
    }

    [Fact]
    public async Task GetPagedAsync_AppliesTheTypeFilter()
    {
        var repository = Resolve<ITransactionRepository>();

        var expenses = await repository.GetPagedAsync(
            UserId, new TransactionQuery { Type = TransactionType.Expense }, 1, 50);

        Assert.Equal(5, expenses.Count);
        Assert.All(expenses, t => Assert.Equal(TransactionType.Expense, t.Type));
    }

    [Fact]
    public async Task GetCategoryTotalsAsync_SumsPerCategory()
    {
        var repository = Resolve<ITransactionRepository>();

        var totals = await repository.GetCategoryTotalsAsync(
            UserId, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        Assert.Equal(175m, Assert.Single(totals, t => t.CategoryId == _foodId).Total);
        Assert.Equal(70m, Assert.Single(totals, t => t.CategoryId == _transportId).Total);
    }

    [Fact]
    public async Task GetCategoryTotalsAsync_ExcludesOtherUsersAndIncome()
    {
        var repository = Resolve<ITransactionRepository>();

        var totals = await repository.GetCategoryTotalsAsync(
            UserId, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        // The other user's 9999 would blow this out; salary income is not spending.
        Assert.DoesNotContain(totals, t => t.Total >= 9999m);
        Assert.Equal(245m, totals.Sum(t => t.Total));
    }

    [Fact]
    public async Task GetCategoryTotalsAsync_RestrictsToTheGivenCategories()
    {
        var repository = Resolve<ITransactionRepository>();

        var onlyTransport = await repository.GetCategoryTotalsAsync(
            UserId, new[] { _transportId }, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        Assert.Equal(70m, Assert.Single(onlyTransport).Total);

        var both = await repository.GetCategoryTotalsAsync(
            UserId, new[] { _transportId, _foodId }, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        Assert.Equal(2, both.Count);
    }

    [Fact]
    public async Task GetCategoryTotalsAsync_ReturnsNothingForAnEmptyCategoryList()
    {
        var repository = Resolve<ITransactionRepository>();

        var totals = await repository.GetCategoryTotalsAsync(
            UserId, Array.Empty<Guid>(), new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        Assert.Empty(totals);
    }

    [Fact]
    public async Task GetMonthlyTotalsAsync_BucketsByCalendarMonth()
    {
        var repository = Resolve<ITransactionRepository>();

        var totals = await repository.GetMonthlyTotalsAsync(
            UserId, new DateTime(2026, 1, 1), new DateTime(2026, 2, 28));

        Assert.Equal(4, totals.Count);
        Assert.Equal(900m, Assert.Single(totals, t => t is { Year: 2026, Month: 1, Type: TransactionType.Income }).Total);
        Assert.Equal(245m, Assert.Single(totals, t => t is { Year: 2026, Month: 1, Type: TransactionType.Expense }).Total);
        Assert.Equal(1000m, Assert.Single(totals, t => t is { Year: 2026, Month: 2, Type: TransactionType.Income }).Total);
        Assert.Equal(200m, Assert.Single(totals, t => t is { Year: 2026, Month: 2, Type: TransactionType.Expense }).Total);
    }

    [Fact]
    public async Task GetMonthlyTotalsAsync_ExcludesOtherUsers()
    {
        var repository = Resolve<ITransactionRepository>();

        var totals = await repository.GetMonthlyTotalsAsync(
            UserId, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        Assert.DoesNotContain(totals, t => t.Total >= 9999m);
    }

    [Fact]
    public async Task CountByDateRangeAsync_CountsInSql()
    {
        var repository = Resolve<ITransactionRepository>();

        Assert.Equal(5, await repository.CountByDateRangeAsync(UserId, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)));
        Assert.Equal(2, await repository.CountByDateRangeAsync(UserId, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28)));
        Assert.Equal(1, await repository.CountByDateRangeAsync(OtherUserId, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)));
    }

    [Fact]
    public async Task GetByIdsAsync_ResolvesManyInOneQuery()
    {
        var repository = Resolve<ICategoryRepository>();

        var categories = await repository.GetByIdsAsync(new[] { _foodId, _salaryId });

        Assert.Equal(2, categories.Count);
        Assert.Equal("Food", categories[_foodId].Name);
        Assert.Equal("Salary", categories[_salaryId].Name);
    }

    [Fact]
    public async Task GetByIdsAsync_ReturnsEmptyWithoutTouchingTheDatabase()
    {
        var repository = Resolve<ICategoryRepository>();

        Assert.Empty(await repository.GetByIdsAsync(Array.Empty<Guid>()));
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}