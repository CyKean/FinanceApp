using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Notifications;
using FinanceApp.Application.Services;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceApp.UnitTests;

public class NotificationFeedBuilderTests
{
    private readonly Guid _userId = Guid.NewGuid();

    private readonly Mock<IBudgetService> _budgets = new();
    private readonly Mock<IFinancialGoalService> _goals = new();
    private readonly Mock<IRecurringTransactionService> _recurring = new();
    private readonly Mock<IPredictionService> _predictions = new();
    private readonly Mock<ISyncService> _sync = new();

    private readonly NotificationFeedBuilder _builder;

    public NotificationFeedBuilderTests()
    {
        _builder = new NotificationFeedBuilder(
            _budgets.Object,
            _goals.Object,
            _recurring.Object,
            _predictions.Object,
            _sync.Object,
            Mock.Of<ILogger<NotificationFeedBuilder>>());

        _budgets.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetDto>());
        _goals.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FinancialGoalDto>());
        _recurring.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecurringTransactionDto>());
        _predictions.Setup(x => x.GeneratePredictionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPrediction());
        _sync.Setup(x => x.GetStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncStatusDto(false, null, 0, 0, null));
    }

    [Fact]
    public async Task BuildAsync_UsesLocalToday_NotUtc()
    {
        DateTime? captured = null;
        _budgets.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTime, CancellationToken>((_, asOf, _) => captured = asOf)
            .ReturnsAsync(Array.Empty<BudgetDto>());

        await _builder.BuildAsync(_userId);

        Assert.NotNull(captured);
        Assert.Equal(DateTime.Today, captured!.Value.Date);
    }

    [Fact]
    public async Task BuildAsync_ReportsBudgetOverLimit()
    {
        var budget = MakeBudget(PercentageUsed: 120, isOver: true, isNear: true);
        _budgets.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { budget });

        var items = await _builder.BuildAsync(_userId);

        var alert = Assert.Single(items, i => i.Id == $"budget-over-{budget.Id}");
        Assert.Equal(NotificationSeverity.Critical, alert.Severity);
        Assert.True(alert.NeedsAttention);
        Assert.Equal("//Budgets", alert.Destination);
    }

    [Fact]
    public async Task BuildAsync_ReportsBudgetNearLimit()
    {
        var budget = MakeBudget(PercentageUsed: 91, isOver: false, isNear: true);
        _budgets.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { budget });

        var items = await _builder.BuildAsync(_userId);

        var alert = Assert.Single(items, i => i.Id == $"budget-near-{budget.Id}");
        Assert.Equal(NotificationSeverity.Warning, alert.Severity);
        Assert.Contains("91% used", alert.Body);
        Assert.Contains("90 left of 1,000", alert.Body);
    }

    [Fact]
    public async Task BuildAsync_IgnoresHealthyBudget()
    {
        _budgets.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeBudget(40, isOver: false, isNear: false) });

        var items = await _builder.BuildAsync(_userId);

        Assert.DoesNotContain(items, i => i.Group == "Budgets");
    }

    [Fact]
    public async Task BuildAsync_ReportsGoalCompleted()
    {
        _goals.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeGoal(progress: 100, daysRemaining: 30) });

        var items = await _builder.BuildAsync(_userId);

        var alert = Assert.Single(items, i => i.Id.StartsWith("goal-done-"));
        Assert.Equal(NotificationSeverity.Success, alert.Severity);
        Assert.False(alert.NeedsAttention);
    }

    [Fact]
    public async Task BuildAsync_ReportsOverdueGoalAsWarning()
    {
        _goals.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeGoal(progress: 40, daysRemaining: -3) });

        var items = await _builder.BuildAsync(_userId);

        var alert = Assert.Single(items, i => i.Id.StartsWith("goal-due-"));
        Assert.Equal(NotificationSeverity.Warning, alert.Severity);
        Assert.Contains("past its target date", alert.Title);
    }

    [Fact]
    public async Task BuildAsync_ReportsOverdueBillAsCritical()
    {
        _recurring.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeBill(DateTime.Today.AddDays(-2)) });

        var items = await _builder.BuildAsync(_userId);

        var alert = Assert.Single(items, i => i.Group == "Bills");
        Assert.Equal(NotificationSeverity.Critical, alert.Severity);
        Assert.Contains("overdue", alert.Title);
    }

    [Fact]
    public async Task BuildAsync_IgnoresBillBeyondHorizon()
    {
        _recurring.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeBill(DateTime.Today.AddDays(30)) });

        var items = await _builder.BuildAsync(_userId);

        Assert.DoesNotContain(items, i => i.Group == "Bills");
    }

    [Fact]
    public async Task BuildAsync_ReportsAnomaliesAsCritical()
    {
        _predictions.Setup(x => x.GeneratePredictionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPrediction() with
            {
                Anomalies = new[]
                {
                    new AnomalyDetectionDto(
                        Guid.NewGuid(),
                        DateTime.Now.AddDays(-1),
                        new Money(5000),
                        new CategoryId(Guid.NewGuid()),
                        "Dining",
                        new Money(100),
                        new Money(500),
                        180,
                        "Far above your usual Dining spend")
                }
            });

        var items = await _builder.BuildAsync(_userId);

        var alert = Assert.Single(items, i => i.Group == "Insights" && i.IconKey == "alertCircle");
        Assert.Equal(NotificationSeverity.Critical, alert.Severity);
        Assert.Contains("Dining", alert.Title);
    }

    [Fact]
    public async Task BuildAsync_ReportsBudgetForecastOvershoot()
    {
        var budgetId = Guid.NewGuid();
        _predictions.Setup(x => x.GeneratePredictionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPrediction() with
            {
                BudgetForecasts = new[]
                {
                    new BudgetForecastDto(
                        budgetId, "Food", new CategoryId(Guid.NewGuid()), "Food",
                        "🍔", "#FF6B6B",
                        new Money(1000), new Money(800), new Money(1500), new Money(-500),
                        80, true, 50)
                }
            });

        var items = await _builder.BuildAsync(_userId);

        var alert = Assert.Single(items, i => i.Id == $"forecast-over-{budgetId}");
        Assert.Equal(NotificationSeverity.Warning, alert.Severity);
    }

    [Fact]
    public async Task BuildAsync_ReportsSyncFailures()
    {
        _sync.Setup(x => x.GetStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncStatusDto(false, DateTime.Now, 0, 3, "Row level security denied"));

        var items = await _builder.BuildAsync(_userId);

        var alert = Assert.Single(items, i => i.Id == "sync-failed");
        Assert.Equal(NotificationSeverity.Critical, alert.Severity);
        Assert.Contains("Row level security denied", alert.Body);
    }

    [Fact]
    public async Task BuildAsync_KeepsOtherSections_WhenOneSourceFails()
    {
        _budgets.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("budget query exploded"));
        _sync.Setup(x => x.GetStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncStatusDto(false, null, 2, 0, null));

        var items = await _builder.BuildAsync(_userId);

        var alert = Assert.Single(items, i => i.Id == "sync-pending");
        Assert.Equal(NotificationSeverity.Info, alert.Severity);
    }

    [Fact]
    public async Task BuildAsync_DeDuplicatesById()
    {
        var budget = MakeBudget(120, isOver: true, isNear: true);
        _budgets.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { budget });

        var first = await _builder.BuildAsync(_userId);
        var second = await _builder.BuildAsync(_userId);

        Assert.Single(first, i => i.Id == $"budget-over-{budget.Id}");
        Assert.Single(second, i => i.Id == $"budget-over-{budget.Id}");
    }

    [Fact]
    public async Task BuildAsync_ReturnsNewestFirst()
    {
        var items = await _builder.BuildAsync(_userId);

        Assert.Equal(items.OrderByDescending(i => i.CreatedAt).ToList(), items);
    }

    private static BudgetDto MakeBudget(decimal PercentageUsed, bool isOver, bool isNear)
    {
        var amount = new Money(1000);
        var spent = new Money(1000 * (PercentageUsed / 100m));

        return new BudgetDto(
            Guid.NewGuid(),
            "Food Budget",
            amount,
            spent,
            amount.Subtract(spent),
            PercentageUsed,
            DateTime.Today.AddDays(-10),
            DateTime.Today.AddDays(10),
            new CategoryId(Guid.NewGuid()),
            "Food",
            "food",
            "#FF6B6B",
            null,
            null,
            isOver,
            isNear,
            SyncStatus.Synced,
            null,
            DateTime.Today.AddMonths(-1),
            DateTime.Today,
            false,
            null);
    }

    private static FinancialGoalDto MakeGoal(decimal progress, int daysRemaining) =>
        new(
            Guid.NewGuid(),
            "Emergency Fund",
            new Money(10000),
            new Money(10000 * (progress / 100m)),
            new Money(10000 - (10000 * (progress / 100m))),
            progress,
            DateTime.Today.AddDays(daysRemaining),
            DateTime.Today.AddMonths(-3),
            GoalStatus.Active,
            null,
            "target",
            "#CDF463",
            null,
            null,
            daysRemaining,
            new Money(500),
            true,
            SyncStatus.Synced,
            null,
            DateTime.Today.AddMonths(-3),
            DateTime.Today,
            false);

    private static RecurringTransactionDto MakeBill(DateTime due) =>
        new(
            Guid.NewGuid(),
            "Internet",
            TransactionType.Expense,
            new Money(1500),
            RecurringFrequency.Monthly,
            DateTime.Today.AddMonths(-2),
            null,
            new AccountId(Guid.NewGuid()),
            "Cash",
            new CategoryId(Guid.NewGuid()),
            "Utilities",
            DateTime.Today.AddDays(-30),
            due,
            true,
            null,
            SyncStatus.Synced,
            null,
            DateTime.Today.AddMonths(-2),
            DateTime.Today,
            false);

    private static PredictionResultDto EmptyPrediction() =>
        new(
            null,
            Array.Empty<SpendingTrendDto>(),
            Array.Empty<BudgetForecastDto>(),
            Array.Empty<SmartInsightDto>(),
            Array.Empty<AnomalyDetectionDto>(),
            DateTime.Now);
}