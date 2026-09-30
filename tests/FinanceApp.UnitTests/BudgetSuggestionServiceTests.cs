using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceApp.UnitTests;

public class BudgetSuggestionServiceTests
{
    private readonly Mock<IDashboardService> _dashboardService = new();
    private readonly Mock<IBudgetService> _budgetService = new();
    private readonly BudgetSuggestionService _service;

    public BudgetSuggestionServiceTests()
    {
        _service = new BudgetSuggestionService(
            _dashboardService.Object,
            _budgetService.Object,
            Mock.Of<ILogger<BudgetSuggestionService>>());
    }

    [Fact]
    public async Task GetSuggestionsAsync_SuggestsCreate_WhenCategoryHasNoBudget()
    {
        var categoryId = Guid.NewGuid();
        SetupAnalytics(Spending(categoryId, "Food", 3000m));
        SetupBudgets(new List<BudgetDto>());

        var result = await _service.GetSuggestionsAsync(Guid.NewGuid());

        var suggestion = Assert.Single(result);
        Assert.Equal(BudgetSuggestionKind.Create, suggestion.Kind);
        Assert.Equal(new Money(1000m), suggestion.SuggestedAmount);
        Assert.Equal(categoryId, suggestion.CategoryId.Value);
        Assert.Null(suggestion.TargetBudgetId);
        Assert.Null(suggestion.CurrentAmount);
    }

    [Fact]
    public async Task GetSuggestionsAsync_SuggestsIncrease_WhenBudgetIsTooLow()
    {
        var categoryId = Guid.NewGuid();
        var budget = CreateBudget(categoryId, "Food budget", 700m);
        SetupAnalytics(Spending(categoryId, "Food", 3000m));
        SetupBudgets(new List<BudgetDto> { budget });

        var result = await _service.GetSuggestionsAsync(Guid.NewGuid());

        var suggestion = Assert.Single(result);
        Assert.Equal(BudgetSuggestionKind.Increase, suggestion.Kind);
        Assert.Equal(new Money(1000m), suggestion.SuggestedAmount);
        Assert.Equal(new Money(700m), suggestion.CurrentAmount);
        Assert.Equal(budget.Id, suggestion.TargetBudgetId);
    }

    [Fact]
    public async Task GetSuggestionsAsync_SuggestsDecrease_WhenBudgetIsTooHigh()
    {
        var categoryId = Guid.NewGuid();
        var budget = CreateBudget(categoryId, "Food budget", 3000m);
        SetupAnalytics(Spending(categoryId, "Food", 6000m));
        SetupBudgets(new List<BudgetDto> { budget });

        var result = await _service.GetSuggestionsAsync(Guid.NewGuid());

        var suggestion = Assert.Single(result);
        Assert.Equal(BudgetSuggestionKind.Decrease, suggestion.Kind);
        Assert.Equal(new Money(2000m), suggestion.SuggestedAmount);
        Assert.Equal(budget.Id, suggestion.TargetBudgetId);
    }

    [Fact]
    public async Task GetSuggestionsAsync_ReturnsEmpty_WhenSpendIsTooSmallToBudget()
    {
        var categoryId = Guid.NewGuid();
        SetupAnalytics(Spending(categoryId, "Coffee", 150m));
        SetupBudgets(new List<BudgetDto>());

        var result = await _service.GetSuggestionsAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSuggestionsAsync_NoSuggestion_WhenBudgetMatchesAverage()
    {
        var categoryId = Guid.NewGuid();
        var budget = CreateBudget(categoryId, "Food budget", 1000m);
        SetupAnalytics(Spending(categoryId, "Food", 3000m));
        SetupBudgets(new List<BudgetDto> { budget });

        var result = await _service.GetSuggestionsAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSuggestionsAsync_ReturnsAtMostFiveSuggestions()
    {
        var categories = new List<CategorySpendingDto>();
        for (var i = 0; i < 8; i++)
            categories.Add(Spending(Guid.NewGuid(), $"Category {i}", 3000m));

        SetupAnalytics(categories.ToArray());
        SetupBudgets(new List<BudgetDto>());

        var result = await _service.GetSuggestionsAsync(Guid.NewGuid());

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task GetSuggestionsAsync_ReturnsEmpty_OnServiceError()
    {
        _dashboardService
            .Setup(x => x.GetAnalyticsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _service.GetSuggestionsAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    private void SetupAnalytics(params CategorySpendingDto[] categories)
    {
        var analytics = new AnalyticsDto(
            new Money(50000m),
            new Money(30000m),
            new Money(20000m),
            categories,
            new List<MonthlyTrendDto>(),
            40m,
            new Money(1000m),
            new Money(10000m),
            categories);

        _dashboardService
            .Setup(x => x.GetAnalyticsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analytics);
    }

    private void SetupBudgets(IReadOnlyList<BudgetDto> budgets)
    {
        _budgetService
            .Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(budgets);
    }

    private static CategorySpendingDto Spending(Guid categoryId, string name, decimal amount) =>
        new(new CategoryId(categoryId), name, "🍔", "#FF6B6B", new Money(amount), 50m);

    private static BudgetDto CreateBudget(Guid categoryId, string name, decimal amount) =>
        new(
            Guid.NewGuid(),
            name,
            new Money(amount),
            new Money(0m),
            new Money(amount),
            0m,
            DateTime.Today.AddDays(-5),
            DateTime.Today.AddDays(25),
            new CategoryId(categoryId),
            "Food",
            "🍔",
            "#FF6B6B",
            "🍔",
            "#FF6B6B",
            false,
            false,
            SyncStatus.Synced,
            null,
            DateTime.Today,
            DateTime.Today,
            false,
            null);
}
