namespace FinanceApp.Application.Services;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

public class BudgetSuggestionService : IBudgetSuggestionService
{
    private const int AnalysisMonths = 3;
    private const decimal IncreaseThreshold = 1.15m;
    private const decimal DecreaseThreshold = 0.85m;
    private const decimal MinSuggestedAmount = 100m;
    private const decimal MinChangeAmount = 50m;
    private const int MaxSuggestions = 5;

    private readonly IDashboardService _dashboardService;
    private readonly IBudgetService _budgetService;
    private readonly ILogger<BudgetSuggestionService> _logger;

    public BudgetSuggestionService(
        IDashboardService dashboardService,
        IBudgetService budgetService,
        ILogger<BudgetSuggestionService> logger)
    {
        _dashboardService = dashboardService;
        _budgetService = budgetService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BudgetSuggestionDto>> GetSuggestionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var analytics = await _dashboardService.GetAnalyticsAsync(userId, AnalysisMonths, cancellationToken);
            var activeBudgets = await _budgetService.GetActiveAsync(userId, DateTime.Today, cancellationToken);
            var budgetsByCategory = activeBudgets
                .GroupBy(b => b.CategoryId.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var suggestions = new List<BudgetSuggestionDto>();

            foreach (var category in analytics.CategoryBreakdown)
            {
                if (category.Amount.Amount <= 0) continue;

                var monthlyAverage = RoundUp(category.Amount.Divide(AnalysisMonths));
                if (monthlyAverage.Amount < MinSuggestedAmount) continue;

                if (!budgetsByCategory.TryGetValue(category.CategoryId.Value, out var budget))
                {
                    suggestions.Add(new BudgetSuggestionDto(
                        Guid.NewGuid(),
                        BudgetSuggestionKind.Create,
                        category.CategoryId,
                        category.CategoryName,
                        category.CategoryIcon,
                        category.CategoryColor,
                        monthlyAverage,
                        null,
                        null,
                        $"You averaged {monthlyAverage} per month over the last {AnalysisMonths} months and have no budget for this category."));
                    continue;
                }

                var currentAmount = budget.Amount;

                if (monthlyAverage > currentAmount.Multiply(IncreaseThreshold) &&
                    monthlyAverage - currentAmount >= new Money(MinChangeAmount, currentAmount.Currency))
                {
                    suggestions.Add(new BudgetSuggestionDto(
                        Guid.NewGuid(),
                        BudgetSuggestionKind.Increase,
                        category.CategoryId,
                        category.CategoryName,
                        category.CategoryIcon,
                        category.CategoryColor,
                        monthlyAverage,
                        currentAmount,
                        budget.Id,
                        $"You averaged {monthlyAverage} per month but this budget is only {currentAmount}, so it is likely too tight."));
                }
                else if (monthlyAverage < currentAmount.Multiply(DecreaseThreshold) &&
                         currentAmount - monthlyAverage >= new Money(MinChangeAmount, currentAmount.Currency))
                {
                    suggestions.Add(new BudgetSuggestionDto(
                        Guid.NewGuid(),
                        BudgetSuggestionKind.Decrease,
                        category.CategoryId,
                        category.CategoryName,
                        category.CategoryIcon,
                        category.CategoryColor,
                        monthlyAverage,
                        currentAmount,
                        budget.Id,
                        $"You averaged {monthlyAverage} per month while this budget is {currentAmount}, so you could free up {currentAmount - monthlyAverage}."));
                }
            }

            return suggestions
                .OrderByDescending(s => s.SuggestedAmount.Amount)
                .Take(MaxSuggestions)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating budget suggestions for user {UserId}", userId);
            return Array.Empty<BudgetSuggestionDto>();
        }
    }

    private static Money RoundUp(Money amount) =>
        new(Math.Ceiling(amount.Amount / 10m) * 10m, amount.Currency);
}
