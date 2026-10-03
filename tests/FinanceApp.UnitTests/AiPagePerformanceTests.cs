using System.Diagnostics;
using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceApp.UnitTests;

/// <summary>
/// Runs the AI pages against a real SQLite database and asserts they stay well
/// inside the budget. <para>
/// There is no async EF Core provider for SQLite, so repository methods marked
/// async complete synchronously on the calling thread. Anything slow here is a
/// UI-thread freeze on the device, which is what produced the "Paytin isn't
/// responding" ANR - so the bound is generous but far below Android's five
/// second input threshold.
/// </para>
/// </summary>
public class AiPagePerformanceTests : IDisposable
{
    /// <summary>
    /// Measured at roughly 40ms for the forecast pipeline plus suggestions
    /// against 168 transactions on this machine. Seeding an empty account adds
    /// several hundred more. The ceiling leaves ample headroom for slower
    /// disks while still catching an accidental N+1 or a reordering that moves
    /// the work back onto the caller's thread.
    /// </summary>
    private const int BudgetMilliseconds = 3000;

    private static DateTime MonthStart(int monthsAgo) =>
        new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-monthsAgo);

    private readonly FinanceAppDbContext _context;

    public AiPagePerformanceTests()
    {
        // A real on-disk SQLite file: an in-memory provider would not show the
        // I/O cost that causes the jank.
        var path = Path.Combine(Path.GetTempPath(), $"financeapp-perf-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<FinanceAppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        _context = new FinanceAppDbContext(options);
        _context.Database.EnsureCreated();
        ContextPath = path;
    }

    private string ContextPath { get; }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        _context.Dispose();

        try
        {
            if (File.Exists(ContextPath))
                File.Delete(ContextPath);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }

    [Fact]
    public void ForecastPipeline_StaysWithinBudget()
    {
        var userId = Guid.NewGuid();
        Seed(userId, months: 12);

        var elapsed = Stopwatch.StartNew();
        PredictionResultDto forecasts;
        try
        {
            forecasts = BuildPredictionService().GeneratePredictionAsync(userId).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Assert.Fail($"THREW: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            throw;
        }
        elapsed.Stop();

        Assert.True(elapsed.ElapsedMilliseconds < BudgetMilliseconds,
            $"Forecast pipeline took {elapsed.ElapsedMilliseconds} ms, budget is {BudgetMilliseconds} ms.");

        // And it must still produce a populated page.
        Assert.NotEmpty(forecasts.ExpensePrediction.CategoryPredictions);
        Assert.NotEmpty(forecasts.SpendingTrends);
        Assert.NotEmpty(forecasts.BudgetForecasts);
    }

    /// <summary>
    /// Every section of the forecasts page has to be reachable, otherwise the
    /// user stares at a titled card with nothing in it. The fixture mirrors the
    /// shape DevDataSeeder writes, including the deliberate outliers.
    /// </summary>
    [Fact]
    public void EveryForecastSection_CanPopulate()
    {
        var userId = Guid.NewGuid();
        Seed(userId, months: 12);

        var forecasts = BuildPredictionService().GeneratePredictionAsync(userId).GetAwaiter().GetResult();

        var report =
            $"categories={forecasts.ExpensePrediction.CategoryPredictions.Count} " +
            $"trends={forecasts.SpendingTrends.Count} " +
            $"budgets={forecasts.BudgetForecasts.Count} " +
            $"insights={forecasts.Insights.Count} " +
            $"anomalies={forecasts.Anomalies.Count} " +
            $"confidence={forecasts.ExpensePrediction.Confidence}";

        Assert.True(forecasts.ExpensePrediction.CategoryPredictions.Count > 0, $"No category predictions. {report}");
        Assert.True(forecasts.SpendingTrends.Count > 0, $"No spending trends. {report}");
        Assert.True(forecasts.BudgetForecasts.Count > 0, $"No budget forecasts. {report}");
        Assert.True(forecasts.Insights.Count > 0, $"No smart insights. {report}");
        Assert.True(forecasts.Anomalies.Count > 0, $"No anomalies. {report}");

        // Each trend row must carry the icon the page binds.
        Assert.All(forecasts.SpendingTrends, t => Assert.False(string.IsNullOrWhiteSpace(t.CategoryIcon)));
        Assert.All(forecasts.BudgetForecasts, b =>
        {
            Assert.False(string.IsNullOrWhiteSpace(b.CategoryIcon));
            Assert.False(string.IsNullOrWhiteSpace(b.CategoryColor));
            Assert.True(b.PercentageUsed > 0);
        });
    }

    [Fact]
    public void SuggestionPipeline_StaysWithinBudget()
    {
        var userId = Guid.NewGuid();
        Seed(userId, months: 12);

        var elapsed = Stopwatch.StartNew();
        var suggestions = BuildSuggestionService().GetSuggestionsAsync(userId).GetAwaiter().GetResult();
        elapsed.Stop();

        Assert.True(elapsed.ElapsedMilliseconds < BudgetMilliseconds,
            $"Suggestion pipeline took {elapsed.ElapsedMilliseconds} ms, budget is {BudgetMilliseconds} ms.");

        Assert.NotEmpty(suggestions);
    }

    private void Seed(Guid userId, int months)
    {
        var random = new Random(42);
        var categories = new List<Category>();

        foreach (var name in new[] { "Salary", "Food", "Transportation", "Housing", "Utilities", "Shopping", "Entertainment" })
        {
            var type = name == "Salary" ? CategoryType.Income : CategoryType.Expense;
            categories.Add(new Category(name, type, userId, "icon", "#FF6B6B", null, false, 0));
        }

        _context.Categories.AddRange(categories);
        _context.SaveChanges();

        var accountId = Guid.NewGuid();
        var transactions = new List<Transaction>();
        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        for (var offset = months - 1; offset >= 0; offset--)
        {
            var start = currentMonth.AddMonths(-offset);
            var lastDay = offset == 0
                ? DateTime.Today.Day
                : DateTime.DaysInMonth(start.Year, start.Month);

            Transaction Add(TransactionType type, decimal amount, int day, string categoryName)
            {
                var category = categories.First(c => c.Name == categoryName);
                var transaction = new Transaction(
                    type, new Money(amount), start.AddDays(Math.Min(day, lastDay) - 1),
                    new AccountId(accountId), new CategoryId(category.Id), userId);
                transactions.Add(transaction);
                return transaction;
            }

            Add(TransactionType.Income, 48000m, 15, "Salary");
            Add(TransactionType.Expense, 12000m, 3, "Housing");
            Add(TransactionType.Expense, 2200m, 8, "Utilities");

            for (var i = 0; i < 3; i++)
                Add(TransactionType.Expense, 3000m, random.Next(1, lastDay + 1), "Food");

            for (var i = 0; i < 5; i++)
                Add(TransactionType.Expense, random.Next(40, 350), random.Next(1, lastDay + 1), "Transportation");

            for (var i = 0; i < 3; i++)
                Add(TransactionType.Expense, random.Next(500, 2500), random.Next(1, lastDay + 1), "Shopping");

            for (var i = 0; i < 2; i++)
                Add(TransactionType.Expense, random.Next(200, 900), random.Next(1, lastDay + 1), "Entertainment");
        }

        // Deliberate outliers, mirroring DevDataSeeder, so the anomaly section
        // has something to report.
        var entertainment = categories.First(c => c.Name == "Entertainment");
        var shopping = categories.First(c => c.Name == "Shopping");
        var outliers = new List<Transaction>
        {
            new(TransactionType.Expense, new Money(18500m), MonthStart(1).AddDays(9),
                new AccountId(accountId), new CategoryId(entertainment.Id), userId),
            new(TransactionType.Expense, new Money(14200m), MonthStart(2).AddDays(14),
                new AccountId(accountId), new CategoryId(shopping.Id), userId)
        };

        transactions.AddRange(outliers);

        _context.Transactions.AddRange(transactions);
        _context.SaveChanges();

        // Budgets are essential to cover the forecast path: without them
        // ForecastBudgetsAsync short-circuits and the per-budget spent
        // recalculation is never exercised.
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var budgets = new[]
        {
            new Budget("Food & Groceries", new Money(18000m), monthStart, monthStart.AddMonths(1).AddDays(-1),
                new CategoryId(categories.First(c => c.Name == "Food").Id), userId),
            new Budget("Transportation", new Money(5000m), monthStart, monthStart.AddMonths(1).AddDays(-1),
                new CategoryId(categories.First(c => c.Name == "Transportation").Id), userId),
            new Budget("Shopping", new Money(5000m), monthStart, monthStart.AddMonths(1).AddDays(-1),
                new CategoryId(categories.First(c => c.Name == "Shopping").Id), userId)
        };

        _context.Budgets.AddRange(budgets);
        _context.SaveChanges();
    }

    private PredictionService BuildPredictionService()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return new PredictionService(
            unitOfWork.Object,
            new TransactionRepository(_context),
            new CategoryRepository(_context),
            BuildBudgetService(unitOfWork),
            new RecurringTransactionRepository(_context),
            Mock.Of<ILogger<PredictionService>>());
    }

    private BudgetService BuildBudgetService(Mock<IUnitOfWork> unitOfWork) =>
        new(unitOfWork.Object,
            new BudgetRepository(_context),
            new CategoryRepository(_context),
            new AccountRepository(_context),
            new TransactionRepository(_context),
            Mock.Of<INotificationService>(),
            new FinanceApp.Application.Validators.CreateBudgetDtoValidator(),
            new FinanceApp.Application.Validators.UpdateBudgetDtoValidator(),
            Mock.Of<ILogger<BudgetService>>());

    private BudgetSuggestionService BuildSuggestionService()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return new BudgetSuggestionService(
            new DashboardService(
                unitOfWork.Object,
                new AccountRepository(_context),
                new TransactionRepository(_context),
                new CategoryRepository(_context),
                new BudgetRepository(_context),
                new FinancialGoalRepository(_context),
                Mock.Of<ILogger<DashboardService>>()),
            BuildBudgetService(unitOfWork),
            Mock.Of<ILogger<BudgetSuggestionService>>());
    }
}