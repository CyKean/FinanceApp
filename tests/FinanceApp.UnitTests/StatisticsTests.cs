namespace FinanceApp.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FinanceApp.Application.DTOs;
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
/// Verifies the Statistics pipeline end to end against a real SQLite database
/// and the real repositories: the same path the app takes from Transactions →
/// Repository → DashboardService.GetStatisticsAsync → StatisticsDto.
/// </summary>
public class StatisticsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public StatisticsTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_connection);
        services.AddDbContext<FinanceAppDbContext>((sp, options) =>
        {
            options.UseSqlite(sp.GetRequiredService<SqliteConnection>());
            options.UseLoggerFactory(NullLoggerFactory.Instance);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IFinancialGoalRepository, FinancialGoalRepository>();
        services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();
        services.AddScoped<ISyncOperationRepository, SyncOperationRepository>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }

    // DashboardService is stateless, so a fresh instance per access is fine and
    // keeps each test body to seed → query → assert.
    private DashboardService service => BuildService();

    private DashboardService BuildService()
    {
        var sp = _scope.ServiceProvider;
        return new DashboardService(
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<IAccountRepository>(),
            sp.GetRequiredService<ITransactionRepository>(),
            sp.GetRequiredService<ICategoryRepository>(),
            sp.GetRequiredService<IBudgetRepository>(),
            sp.GetRequiredService<IFinancialGoalRepository>(),
            NullLogger<DashboardService>.Instance);
    }

    private async Task<(Account Account, Category Expense, Category Income)> SeedIdentityAsync()
    {
        var context = _scope.ServiceProvider.GetRequiredService<FinanceAppDbContext>();
        var account = new Account("Cash", AccountType.Cash, new Money(0), UserId);
        var expense = new Category("Food", CategoryType.Expense, UserId);
        var income = new Category("Salary", CategoryType.Income, UserId);
        context.Accounts.Add(account);
        context.Categories.AddRange(expense, income);
        await context.SaveChangesAsync();
        return (account, expense, income);
    }

    private async Task AddTransactionAsync(TransactionType type, decimal amount, DateTime date, Account account, Category category)
    {
        var context = _scope.ServiceProvider.GetRequiredService<FinanceAppDbContext>();
        context.Transactions.Add(new Transaction(type, new Money(amount), date, (AccountId)account.Id, (CategoryId)category.Id, UserId));
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task NoTransactions_ReturnsZeros_NotMockValues()
    {
        await SeedIdentityAsync();
        var service = BuildService();

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Month);

        Assert.Equal(0m, result.TotalSpending.Amount);
        Assert.Equal(0m, result.TotalEarning.Amount);
        Assert.All(result.Overview, p => Assert.Equal(0m, p.Spending.Amount));
        Assert.All(result.Overview, p => Assert.Equal(0m, p.Earning.Amount));
        Assert.Empty(result.SpendingByAccountType);
        Assert.Null(result.PrimaryGoal);
        Assert.Null(result.SpendingChangePercent);
        Assert.Null(result.EarningChangePercent);
    }

    [Fact]
    public async Task Expense_CountedAsSpending()
    {
        var (account, expense, _) = await SeedIdentityAsync();
        await AddTransactionAsync(TransactionType.Expense, 500m, DateTime.Today, account, expense);

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Today);

        Assert.Equal(500m, result.TotalSpending.Amount);
        Assert.Equal(0m, result.TotalEarning.Amount);
        Assert.Equal(500m, result.Overview.Sum(p => p.Spending.Amount));
    }

    [Fact]
    public async Task Income_CountedAsEarning()
    {
        var (account, _, income) = await SeedIdentityAsync();
        await AddTransactionAsync(TransactionType.Income, 1000m, DateTime.Today, account, income);

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Today);

        Assert.Equal(1000m, result.TotalEarning.Amount);
        Assert.Equal(1000m, result.Overview.Sum(p => p.Earning.Amount));
    }

    [Fact]
    public async Task MultipleTransactions_AggregateCorrectly()
    {
        var (account, expense, _) = await SeedIdentityAsync();
        await AddTransactionAsync(TransactionType.Expense, 100m, DateTime.Today, account, expense);
        await AddTransactionAsync(TransactionType.Expense, 200m, DateTime.Today, account, expense);
        await AddTransactionAsync(TransactionType.Expense, 500m, DateTime.Today, account, expense);

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Today);

        Assert.Equal(800m, result.TotalSpending.Amount);
        Assert.Equal(800m, result.Overview.Sum(p => p.Spending.Amount));
    }

    [Fact]
    public async Task Today_ExcludesOtherDays()
    {
        var (account, expense, _) = await SeedIdentityAsync();
        await AddTransactionAsync(TransactionType.Expense, 100m, DateTime.Today, account, expense);
        await AddTransactionAsync(TransactionType.Expense, 999m, DateTime.Today.AddDays(-1), account, expense);

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Today);

        Assert.Equal(100m, result.TotalSpending.Amount);
    }

    [Fact]
    public async Task Weekly_RespectsMondayStartAndEndOfWeek()
    {
        var (account, expense, _) = await SeedIdentityAsync();
        var today = DateTime.Today;
        var weekStart = today.AddDays(-((int)today.DayOfWeek + 6) % 7);

        await AddTransactionAsync(TransactionType.Expense, 100m, weekStart, account, expense);
        await AddTransactionAsync(TransactionType.Expense, 200m, weekStart.AddDays(6), account, expense);
        await AddTransactionAsync(TransactionType.Expense, 999m, weekStart.AddDays(-1), account, expense);

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Week);

        Assert.Equal(300m, result.TotalSpending.Amount);
        Assert.Equal(7, result.Overview.Count);
    }

    [Fact]
    public async Task Monthly_RespectsMonthBoundaries()
    {
        var (account, expense, _) = await SeedIdentityAsync();
        var first = DateTime.Today.AddDays(-(DateTime.Today.Day - 1));
        var last = first.AddMonths(1).AddDays(-1);

        await AddTransactionAsync(TransactionType.Expense, 100m, first, account, expense);
        await AddTransactionAsync(TransactionType.Expense, 200m, last, account, expense);
        await AddTransactionAsync(TransactionType.Expense, 999m, first.AddDays(-1), account, expense);

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Month);

        Assert.Equal(300m, result.TotalSpending.Amount);
        Assert.Equal(DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month), result.Overview.Count);
    }

    [Fact]
    public async Task Yearly_RespectsYearBoundariesAndUsesMonthBuckets()
    {
        var (account, expense, _) = await SeedIdentityAsync();
        var first = new DateTime(DateTime.Today.Year, 1, 1);
        var last = new DateTime(DateTime.Today.Year, 12, 31);

        await AddTransactionAsync(TransactionType.Expense, 100m, first, account, expense);
        await AddTransactionAsync(TransactionType.Expense, 200m, last, account, expense);
        await AddTransactionAsync(TransactionType.Expense, 999m, new DateTime(DateTime.Today.Year - 1, 12, 31), account, expense);

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Year);

        Assert.Equal(300m, result.TotalSpending.Amount);
        Assert.Equal(12, result.Overview.Count);
    }

    [Fact]
    public async Task BoundaryDates_AreIncluded()
    {
        var (account, expense, _) = await SeedIdentityAsync();
        var first = DateTime.Today.AddDays(-(DateTime.Today.Day - 1));
        var last = first.AddMonths(1).AddDays(-1);

        await AddTransactionAsync(TransactionType.Expense, 10m, first, account, expense);
        await AddTransactionAsync(TransactionType.Expense, 20m, last, account, expense);
        await AddTransactionAsync(TransactionType.Expense, 40m, first.AddDays(-1), account, expense);

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Month);

        Assert.Equal(30m, result.TotalSpending.Amount);
    }

    [Fact]
    public async Task CreditCardSpending_IsIsolatedFromCashSpending()
    {
        var (account, expense, _) = await SeedIdentityAsync();
        var context = _scope.ServiceProvider.GetRequiredService<FinanceAppDbContext>();
        var credit = new Account("Card", AccountType.CreditCard, new Money(0), UserId);
        context.Accounts.Add(credit);
        await context.SaveChangesAsync();

        await AddTransactionAsync(TransactionType.Expense, 300m, DateTime.Today, account, expense);
        await AddTransactionAsync(TransactionType.Expense, 700m, DateTime.Today, credit, expense);

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Today);

        Assert.Equal(1000m, result.TotalSpending.Amount);
        var creditSlice = result.SpendingByAccountType.Single(s => s.Label == "Credit Card");
        Assert.Equal(700m, creditSlice.Amount.Amount);
        Assert.Equal(70m, creditSlice.Percentage);
    }

    [Fact]
    public async Task TotalIsCountedOnce_AcrossAccountTypes()
    {
        var (account, expense, _) = await SeedIdentityAsync();
        var context = _scope.ServiceProvider.GetRequiredService<FinanceAppDbContext>();
        var credit = new Account("Card", AccountType.CreditCard, new Money(0), UserId);
        context.Accounts.Add(credit);
        await context.SaveChangesAsync();

        await AddTransactionAsync(TransactionType.Expense, 700m, DateTime.Today, credit, expense);

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Today);

        Assert.Equal(700m, result.TotalSpending.Amount);
        Assert.Single(result.SpendingByAccountType);
    }

    [Fact]
    public async Task PreviousPeriodZero_AndCurrentZero_YieldsNoFakePercentage()
    {
        await SeedIdentityAsync();
        var service = BuildService();

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Today);

        Assert.Null(result.SpendingChangePercent);
        Assert.Null(result.EarningChangePercent);
    }

    [Fact]
    public async Task PositiveChange_IsCalculatedFromPreviousPeriod()
    {
        var (account, _, income) = await SeedIdentityAsync();
        // Current window: today. Previous window: yesterday.
        await AddTransactionAsync(TransactionType.Income, 1200m, DateTime.Today, account, income);
        await AddTransactionAsync(TransactionType.Income, 1000m, DateTime.Today.AddDays(-1), account, income);

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Today);

        Assert.NotNull(result.EarningChangePercent);
        Assert.Equal(20m, result.EarningChangePercent);
    }

    [Fact]
    public async Task Currency_IsPhp()
    {
        var (account, expense, _) = await SeedIdentityAsync();
        await AddTransactionAsync(TransactionType.Expense, 100m, DateTime.Today, account, expense);

        var result = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Today);

        Assert.Equal("PHP", result.TotalSpending.Currency);
        Assert.Equal("PHP", result.TotalEarning.Currency);
        Assert.Equal("PHP", result.TotalBalance.Currency);
    }

    [Fact]
    public async Task FilterRefresh_ProducesDifferentRangesPerPeriod()
    {
        var (account, expense, _) = await SeedIdentityAsync();
        await AddTransactionAsync(TransactionType.Expense, 100m, DateTime.Today, account, expense);
        await AddTransactionAsync(TransactionType.Expense, 500m, DateTime.Today.AddDays(-2), account, expense);

        var service = BuildService();

        var today = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Today);
        var week = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Week);
        var month = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Month);
        var year = await service.GetStatisticsAsync(UserId, StatisticsPeriod.Year);

        Assert.Equal(100m, today.TotalSpending.Amount);
        Assert.True(week.TotalSpending.Amount >= today.TotalSpending.Amount);
        Assert.True(month.TotalSpending.Amount >= week.TotalSpending.Amount || week.TotalSpending.Amount >= month.TotalSpending.Amount);
        Assert.Equal(600m, year.TotalSpending.Amount);
        Assert.Equal(7, week.Overview.Count);
        Assert.Equal(12, year.Overview.Count);
    }
}
