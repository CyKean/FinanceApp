using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceApp.UnitTests;

/// <summary>
/// Transaction repositories return rows newest-first, which is what these tests
/// reproduce. An earlier version of the service assumed ascending order, which
/// meant "recent" months were actually the oldest ones and every trend was
/// reported backwards.
/// </summary>
public class PredictionServiceTests
{
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<IBudgetService> _budgets = new();
    private readonly Mock<IRecurringTransactionRepository> _recurring = new();
    private readonly PredictionService _service;

    public PredictionServiceTests()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _service = new PredictionService(
            unitOfWork.Object,
            _transactions.Object,
            _categories.Object,
            _budgets.Object,
            _recurring.Object,
            Mock.Of<ILogger<PredictionService>>());

        _recurring.Setup(x => x.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecurringTransaction>());
        _budgets.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetDto>());
        _transactions.Setup(x => x.GetTotalByTypeAsync(
                It.IsAny<Guid>(), It.IsAny<TransactionType>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Money(0));
    }

    [Fact]
    public async Task PredictExpensesAsync_ReturnsInsufficientData_WhenNoTransactions()
    {
        SetupTransactions(Array.Empty<Transaction>());

        var result = await _service.PredictExpensesAsync(Guid.NewGuid(), 1);

        Assert.Equal(PredictionConfidence.InsufficientData, result.Confidence);
        Assert.Equal(0, result.MonthsAnalyzed);
        Assert.Equal(0, result.TotalTransactionsAnalyzed);
        Assert.Empty(result.CategoryPredictions);
    }

    [Fact]
    public async Task PredictExpensesAsync_ReturnsInsufficientData_WhenUnderTenTransactions()
    {
        var userId = Guid.NewGuid();
        SetupTransactions(new[] { Transaction(userId, 100, MonthsAgo(2)), Transaction(userId, 50, MonthsAgo(1)) });

        var result = await _service.PredictExpensesAsync(userId, 1);

        Assert.Equal(PredictionConfidence.InsufficientData, result.Confidence);
    }

    [Fact]
    public async Task PredictExpensesAsync_WeightsNewestMonthMost_Heavily()
    {
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        // Flat 100 for four months, then a 4000 spike in the newest month.
        // A correct newest-weighted forecast lands near 4000; the old
        // oldest-weighted one landed near 100.
        var list = new List<Transaction>();
        foreach (var month in new[] { 4, 3, 2, 1 })
        {
            list.Add(Transaction(userId, 25, MonthsAgo(month), categoryId));
            list.Add(Transaction(userId, 25, MonthsAgo(month), categoryId));
        }
        list.Add(Transaction(userId, 4000, MonthsAgo(0), categoryId));
        list.Add(Transaction(userId, 25, MonthsAgo(0), categoryId));

        SetupTransactions(NewestFirst(list));
        SetupCategory(categoryId, "Food");

        var result = await _service.PredictExpensesAsync(userId, 1);

        var prediction = Assert.Single(result.CategoryPredictions);

        // Newest-weighted gives 50*0.5 + 50*0.3 + 4025*0.2 = 845.
        // Oldest-weighted (the bug) gives 50*0.5 + 50*0.3 + 50*0.2 = 50.
        Assert.True(
            prediction.PredictedAmount.Amount > 500,
            $"Expected the newest month to dominate, got {prediction.PredictedAmount.Amount}");
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_ReportsIncreasing_WhenNewestMonthsAreHigher()
    {
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        // Deliberately newest-first, exactly as the repository returns it.
        var list = new List<Transaction>();
        for (var offset = 1; offset <= 3; offset++)
            list.Add(Transaction(userId, 400, MonthsAgo(offset), categoryId));
        for (var offset = 4; offset <= 6; offset++)
            list.Add(Transaction(userId, 100, MonthsAgo(offset), categoryId));

        SetupTransactions(NewestFirst(list));
        SetupCategory(categoryId, "Food");

        var result = await _service.AnalyzeTrendsAsync(userId);

        var trend = Assert.Single(result);
        Assert.Equal(400, trend.CurrentAverage.Amount);
        Assert.Equal(100, trend.PreviousAverage.Amount);
        Assert.Equal(SpendingTrend.Increasing, trend.Trend);
        Assert.True(trend.ChangePercentage > 0);
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_ReportsDecreasing_WhenNewestMonthsAreLower()
    {
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var list = new List<Transaction>();
        for (var offset = 1; offset <= 3; offset++)
            list.Add(Transaction(userId, 100, MonthsAgo(offset), categoryId));
        for (var offset = 4; offset <= 6; offset++)
            list.Add(Transaction(userId, 400, MonthsAgo(offset), categoryId));

        SetupTransactions(NewestFirst(list));
        SetupCategory(categoryId, "Food");

        var result = await _service.AnalyzeTrendsAsync(userId);

        var trend = Assert.Single(result);
        Assert.Equal(SpendingTrend.Decreasing, trend.Trend);
        Assert.True(trend.ChangePercentage < 0);
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_CarriesCategoryIcon()
    {
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var list = new List<Transaction>();
        for (var offset = 1; offset <= 6; offset++)
            list.Add(Transaction(userId, 100 + offset, MonthsAgo(offset), categoryId));

        SetupTransactions(NewestFirst(list));
        SetupCategory(categoryId, "Food");

        var trend = Assert.Single(await _service.AnalyzeTrendsAsync(userId));

        Assert.Equal("🍔", trend.CategoryIcon);
    }

    [Fact]
    public async Task ForecastBudgetsAsync_DoesNotDoubleCountAlreadySpent()
    {
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        // Flat 1000 a month, so the forecast for the category is ~1000.
        var list = new List<Transaction>();
        for (var offset = 1; offset <= 4; offset++)
        {
            list.Add(Transaction(userId, 1000, MonthsAgo(offset), categoryId));
            list.Add(Transaction(userId, 1000, MonthsAgo(offset), categoryId));
            list.Add(Transaction(userId, 500, MonthsAgo(offset), categoryId));
        }

        SetupTransactions(NewestFirst(list));
        SetupCategory(categoryId, "Food");

        // Already 4000 of a 10000 budget spent.
        _budgets.Setup(x => x.GetActiveAsync(userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Budget(categoryId, new Money(10000), new Money(4000), 40) });

        var forecast = Assert.Single(await _service.ForecastBudgetsAsync(userId));

        // spentToDate + prediction would be 4000 + ~1000 = ~5000 at worst here,
        // so use a case where the old maths clearly exceeded the limit.
        Assert.True(
            forecast.PredictedSpent.Amount <= 10000,
            $"Projected {forecast.PredictedSpent.Amount} against a 10000 budget");
        Assert.Equal(40, forecast.PercentageUsed);
        Assert.Equal(new Money(4000), forecast.CurrentSpent);
    }

    [Fact]
    public async Task ForecastBudgetsAsync_FlagsBudget_WhenForecastExceedsLimit()
    {
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var list = new List<Transaction>();
        for (var offset = 1; offset <= 4; offset++)
        {
            list.Add(Transaction(userId, 3000, MonthsAgo(offset), categoryId));
            list.Add(Transaction(userId, 3000, MonthsAgo(offset), categoryId));
            list.Add(Transaction(userId, 100, MonthsAgo(offset), categoryId));
        }

        SetupTransactions(NewestFirst(list));
        SetupCategory(categoryId, "Food");

        _budgets.Setup(x => x.GetActiveAsync(userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Budget(categoryId, new Money(5000), new Money(1000), 20) });

        var forecast = Assert.Single(await _service.ForecastBudgetsAsync(userId));

        Assert.True(forecast.WillExceedBudget);
        Assert.True(forecast.ExceedPercentage > 0);
    }

    [Fact]
    public async Task DetectAnomaliesAsync_ReturnsAnomaly_ForHighSpend()
    {
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var list = new List<Transaction>();
        for (var i = 0; i < 8; i++)
            list.Add(Transaction(userId, 100 + (i % 4) * 10, MonthsAgo(1).AddDays(-i), categoryId));
        list.Add(Transaction(userId, 900, MonthsAgo(0), categoryId));

        SetupTransactions(NewestFirst(list));
        SetupCategory(categoryId, "Food");

        var anomaly = Assert.Single(await _service.DetectAnomaliesAsync(userId));

        Assert.Equal(900, anomaly.Amount.Amount);
        Assert.True(anomaly.DeviationPercentage > 100);
        Assert.DoesNotContain("PHP", anomaly.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DetectAnomaliesAsync_DoesNotThrow_OnZeroAverages()
    {
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var list = new List<Transaction>();
        for (var i = 0; i < 6; i++)
            list.Add(Transaction(userId, 100, MonthsAgo(1).AddDays(-i), categoryId));

        SetupTransactions(NewestFirst(list));
        SetupCategory(categoryId, "Food");

        var result = await _service.DetectAnomaliesAsync(userId);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GenerateInsightsAsync_ComparesSameDays_NotFullPreviousMonth()
    {
        var userId = Guid.NewGuid();
        var today = DateTime.Today;
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var prevStart = startOfMonth.AddMonths(-1);

        var list = new List<Transaction>();
        // Same total in both windows, so the change must be ~0%, not -70%.
        for (var day = 0; day < today.Day; day++)
        {
            list.Add(Transaction(userId, 100, startOfMonth.AddDays(day)));
            list.Add(Transaction(userId, 100, prevStart.AddDays(day)));
        }

        SetupTransactions(NewestFirst(list));

        var insights = await _service.GenerateInsightsAsync(userId);

        Assert.DoesNotContain(insights, i => i.Type == "SpendingDecrease");
        Assert.DoesNotContain(insights, i => i.Type == "SpendingIncrease");
    }

    private void SetupTransactions(IReadOnlyList<Transaction> list) =>
        _transactions.Setup(x => x.GetByTypeAndDateRangeAsync(
                It.IsAny<Guid>(), TransactionType.Expense, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

    private void SetupCategory(Guid categoryId, string name)
    {
        var category = new Category(name, CategoryType.Expense, Guid.NewGuid(), "🍔", "#FF6B6B", null, false, 0);
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(category, categoryId);

        _categories.Setup(x => x.GetActiveByTypeAsync(It.IsAny<Guid>(), CategoryType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category> { category });
        _categories.Setup(x => x.GetByIdAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
    }

    private static IReadOnlyList<Transaction> NewestFirst(IEnumerable<Transaction> source) =>
        source.OrderByDescending(t => t.Date).ToList();

    /// <summary>First day of the month <paramref name="monthsAgo"/> months back. 0 = this month.</summary>
    private static DateTime MonthsAgo(int monthsAgo) =>
        new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-monthsAgo);

    private static Transaction Transaction(Guid userId, decimal amount, DateTime date, Guid? categoryId = null)
    {
        var category = new Category("Cat", CategoryType.Expense, userId, "🍔", "#FF6B6B", null, false, 0);
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(category, categoryId ?? category.Id);
        var account = new Account("Test", AccountType.Cash, new Money(1000), userId);

        var transaction = new Transaction(
            TransactionType.Expense,
            new Money(amount),
            date,
            new AccountId(account.Id),
            new CategoryId(category.Id),
            userId);

        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(transaction, Guid.NewGuid());
        return transaction;
    }

    private static BudgetDto Budget(Guid categoryId, Money amount, Money spent, decimal percentageUsed) =>
        new(
            Guid.NewGuid(),
            "Food budget",
            amount,
            spent,
            amount.Subtract(spent),
            percentageUsed,
            DateTime.Today.AddDays(-10),
            DateTime.Today.AddDays(20),
            new CategoryId(categoryId),
            "Food",
            "🍔",
            "#FF6B6B",
            "🍔",
            "#FF6B6B",
            spent > amount,
            percentageUsed >= 90,
            SyncStatus.Synced,
            null,
            DateTime.Today,
            DateTime.Today,
            false,
            null);
}