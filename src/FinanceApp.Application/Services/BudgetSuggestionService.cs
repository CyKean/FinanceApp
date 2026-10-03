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

            // Overlapping budget periods can exist for one category, so sum
            // them rather than silently picking an arbitrary winner.
            var budgetsByCategory = activeBudgets
                .GroupBy(b => b.CategoryId.Value)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.Amount.Amount).First());

            // The analytics window spans whole calendar months, so it is
            // usually not AnalysisMonths long. Dividing by the requested count
            // under-stated the monthly average by up to 3x depending on the day.
            var monthsInPeriod = analytics.MonthsInPeriod > 0 ? analytics.MonthsInPeriod : AnalysisMonths;
            var periodText = $"{analytics.DaysInPeriod} days";

            var suggestions = new List<BudgetSuggestionDto>();

            foreach (var category in analytics.CategoryBreakdown)
            {
                if (category.Amount.Amount <= 0) continue;

                var monthlyAverage = RoundUp(ScaledToMonths(category.Amount, monthsInPeriod));
                if (monthlyAverage.Amount < MinSuggestedAmount) continue;

                var id = SuggestionId(category.CategoryId.Value, monthlyAverage.Amount);

                if (!budgetsByCategory.TryGetValue(category.CategoryId.Value, out var budget))
                {
                    suggestions.Add(new BudgetSuggestionDto(
                        id,
                        BudgetSuggestionKind.Create,
                        category.CategoryId,
                        category.CategoryName,
                        category.CategoryIcon,
                        category.CategoryColor,
                        monthlyAverage,
                        null,
                        null,
                        $"You averaged {monthlyAverage.Amount:N0} per month over the last {periodText} and have no budget for this category."));
                    continue;
                }

                var currentAmount = budget.Amount;

                if (monthlyAverage > currentAmount.Multiply(IncreaseThreshold) &&
                    monthlyAverage - currentAmount >= new Money(MinChangeAmount, currentAmount.Currency))
                {
                    suggestions.Add(new BudgetSuggestionDto(
                        id,
                        BudgetSuggestionKind.Increase,
                        category.CategoryId,
                        category.CategoryName,
                        category.CategoryIcon,
                        category.CategoryColor,
                        monthlyAverage,
                        currentAmount,
                        budget.Id,
                        $"You averaged {monthlyAverage.Amount:N0} per month over the last {periodText}, but this budget is {currentAmount.Amount:N0}, so it is likely too tight."));
                }
                else if (monthlyAverage < currentAmount.Multiply(DecreaseThreshold) &&
                         currentAmount - monthlyAverage >= new Money(MinChangeAmount, currentAmount.Currency))
                {
                    suggestions.Add(new BudgetSuggestionDto(
                        id,
                        BudgetSuggestionKind.Decrease,
                        category.CategoryId,
                        category.CategoryName,
                        category.CategoryIcon,
                        category.CategoryColor,
                        monthlyAverage,
                        currentAmount,
                        budget.Id,
                        $"You averaged {monthlyAverage.Amount:N0} per month over the last {periodText} while this budget is {currentAmount.Amount:N0}, so you could free up {(currentAmount - monthlyAverage).Amount:N0}."));
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

    /// <summary>
    /// Stable key per category and proposed amount, so dismissing a suggestion
    /// stays dismissed across reloads instead of resurfacing on every page load.
    /// </summary>
    private static string SuggestionId(Guid categoryId, decimal suggestedAmount) =>
        $"budget-suggestion:{categoryId:N}:{suggestedAmount:0.00}";

    private static Money ScaledToMonths(Money amount, decimal monthsInPeriod) =>
        new(Math.Round(amount.Amount / monthsInPeriod, 2), amount.Currency);

    private static Money RoundUp(Money amount) =>
        new(Math.Ceiling(amount.Amount / 10m) * 10m, amount.Currency);
}