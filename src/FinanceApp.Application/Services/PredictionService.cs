namespace FinanceApp.Application.Services;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

public class PredictionService : BaseService, IPredictionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBudgetService _budgetService;
    private readonly IRecurringTransactionRepository _recurringRepository;
    private readonly ILogger<PredictionService> _logger;

    private const int MinMonthsForPrediction = 2;
    private const int MinTransactionsForPrediction = 10;

    /// <summary>Months of history behind the category forecast.</summary>
    private const int ForecastHistoryMonths = 12;

    /// <summary>Months of history behind the trend comparison.</summary>
    private const int TrendHistoryMonths = 6;

    public PredictionService(
        IUnitOfWork unitOfWork,
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IBudgetService budgetService,
        IRecurringTransactionRepository recurringRepository,
        ILogger<PredictionService> logger) : base(unitOfWork, logger)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _budgetService = budgetService;
        _recurringRepository = recurringRepository;
        _logger = logger;
    }

    public async Task<PredictionResultDto> GeneratePredictionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Computed once and shared: ForecastBudgetsAsync needs the same category
        // prediction, and recomputing it doubled every query on this page.
        var expensePrediction = await PredictExpensesAsync(userId, 1, cancellationToken);
        var trends = await AnalyzeTrendsAsync(userId, cancellationToken);
        var budgetForecasts = await ForecastBudgetsCoreAsync(userId, expensePrediction, cancellationToken);
        var insights = await GenerateInsightsAsync(userId, cancellationToken);
        var anomalies = await DetectAnomaliesAsync(userId, cancellationToken);

        return new PredictionResultDto(
            expensePrediction,
            trends,
            budgetForecasts,
            insights,
            anomalies,
            DateTime.Now);
    }

    public async Task<ExpensePredictionDto> PredictExpensesAsync(Guid userId, int monthsAhead, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var startDate = today.AddMonths(-ForecastHistoryMonths);

        var expenses = await _transactionRepository.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, startDate, today, cancellationToken);

        var activeExpenses = expenses.Where(e => !e.IsDeleted).ToList();

        if (!HasSufficientData(activeExpenses))
        {
            return new ExpensePredictionDto(
                Money.Zero("PHP"),
                null,
                null,
                PredictionConfidence.InsufficientData,
                new List<CategoryPredictionDto>(),
                today,
                0,
                activeExpenses.Count);
        }

        var monthlyTotals = MonthlyTotals(activeExpenses);
        var monthsAnalyzed = monthlyTotals.Count;

        var categories = await _categoryRepository.GetActiveByTypeAsync(userId, CategoryType.Expense, cancellationToken);

        // Recurring rows are fetched once for the whole run rather than once per
        // category: the previous per-category query ran N identical times.
        var recurringByCategory = await GetRecurringMonthlyTotalsAsync(userId, cancellationToken);
        var categoryPredictions = new List<CategoryPredictionDto>();

        foreach (var category in categories)
        {
            var catExpenses = activeExpenses.Where(e => e.CategoryId.Value == category.Id).ToList();
            if (catExpenses.Count == 0)
                continue;

            var prediction = CalculateCategoryPrediction(MonthlyTotals(catExpenses));
            var recurringAmount = recurringByCategory.TryGetValue(category.Id, out var recurring)
                ? recurring
                : Money.Zero("PHP");

            categoryPredictions.Add(new CategoryPredictionDto(
                new CategoryId(category.Id),
                category.Name,
                category.Icon ?? "",
                category.Color ?? "",
                prediction.predicted.Add(recurringAmount),
                prediction.min.Add(recurringAmount),
                prediction.max.Add(recurringAmount)));
        }

        var totalPredicted = categoryPredictions.Aggregate(Money.Zero("PHP"), (sum, cp) => sum.Add(cp.PredictedAmount));
        var minTotal = categoryPredictions.Aggregate(Money.Zero("PHP"), (sum, cp) => sum.Add(cp.MinAmount ?? Money.Zero("PHP")));
        var maxTotal = categoryPredictions.Aggregate(Money.Zero("PHP"), (sum, cp) => sum.Add(cp.MaxAmount ?? Money.Zero("PHP")));

        var confidence = CalculateConfidence(monthsAnalyzed, activeExpenses.Count, monthlyTotals);

        return new ExpensePredictionDto(
            totalPredicted,
            minTotal.Amount > 0 ? minTotal : null,
            maxTotal.Amount > 0 ? maxTotal : null,
            confidence,
            categoryPredictions,
            today,
            monthsAnalyzed,
            activeExpenses.Count);
    }

    public async Task<IReadOnlyList<SpendingTrendDto>> AnalyzeTrendsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var startDate = today.AddMonths(-TrendHistoryMonths);

        var expenses = await _transactionRepository.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, startDate, today, cancellationToken);

        var activeExpenses = expenses.Where(e => !e.IsDeleted).ToList();
        var categories = await _categoryRepository.GetActiveByTypeAsync(userId, CategoryType.Expense, cancellationToken);
        var trends = new List<SpendingTrendDto>();

        foreach (var category in categories)
        {
            var catExpenses = activeExpenses.Where(e => e.CategoryId.Value == category.Id).ToList();
            if (catExpenses.Count < 3)
                continue;

            // Oldest-first, so the tail is genuinely the most recent months and
            // the head is the baseline to compare against. A trend needs at
            // least one month of history to compare against, hence four.
            var monthlyTotals = MonthlyTotals(catExpenses);
            if (monthlyTotals.Count < 4)
                continue;

            var recentAvg = monthlyTotals.TakeLast(3).Average(x => x.Total);
            var olderAvg = monthlyTotals.Take(monthlyTotals.Count - 3).Average(x => x.Total);

            if (olderAvg <= 0)
                continue;

            var changePercent = Math.Round(((recentAvg - olderAvg) / olderAvg) * 100, 2);
            var trend = changePercent > 5 ? SpendingTrend.Increasing :
                       changePercent < -5 ? SpendingTrend.Decreasing :
                       SpendingTrend.Stable;

            trends.Add(new SpendingTrendDto(
                new CategoryId(category.Id),
                category.Name,
                category.Icon ?? "",
                trend,
                changePercent,
                new Money(recentAvg, "PHP"),
                new Money(olderAvg, "PHP")));
        }

        return trends.OrderByDescending(t => Math.Abs(t.ChangePercentage)).ToList();
    }

    public async Task<IReadOnlyList<BudgetForecastDto>> ForecastBudgetsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var expensePrediction = await PredictExpensesAsync(userId, 1, cancellationToken);
        return await ForecastBudgetsCoreAsync(userId, expensePrediction, cancellationToken);
    }

    private async Task<IReadOnlyList<BudgetForecastDto>> ForecastBudgetsCoreAsync(
        Guid userId,
        ExpensePredictionDto expensePrediction,
        CancellationToken cancellationToken)
    {
        // IBudgetService (not the repository) so SpentAmount is recalculated
        // from the transactions rather than read from a possibly stale column.
        var activeBudgets = await _budgetService.GetActiveAsync(userId, DateTime.Today, cancellationToken);
        var forecasts = new List<BudgetForecastDto>();
        var categoryPredictions = expensePrediction.CategoryPredictions.ToDictionary(cp => cp.CategoryId, cp => cp);

        foreach (var budget in activeBudgets)
        {
            if (!categoryPredictions.TryGetValue(budget.CategoryId, out var prediction))
                continue;

            // The category prediction is the expected spend for a whole month, so
            // it is the projected end-of-period total in its own right - never
            // something to add on top of what is already spent, which used to
            // roughly double every forecast.
            var projected = budget.SpentAmount.Amount >= prediction.PredictedAmount.Amount
                ? budget.SpentAmount
                : prediction.PredictedAmount;

            var predictedRemaining = budget.Amount.Subtract(projected);
            var willExceed = projected > budget.Amount;
            var exceedPercent = budget.Amount.Amount > 0
                ? Math.Round(((projected.Amount - budget.Amount.Amount) / budget.Amount.Amount) * 100, 2)
                : 0;

            forecasts.Add(new BudgetForecastDto(
                budget.Id,
                budget.Name,
                budget.CategoryId,
                budget.CategoryName,
                budget.CategoryIcon,
                budget.CategoryColor,
                budget.Amount,
                budget.SpentAmount,
                projected,
                predictedRemaining,
                budget.PercentageUsed,
                willExceed,
                exceedPercent));
        }

        return forecasts;
    }

    public async Task<IReadOnlyList<SmartInsightDto>> GenerateInsightsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var insights = new List<SmartInsightDto>();
        var today = DateTime.Today;
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var prevMonthStart = startOfMonth.AddMonths(-1);

        // Compare like with like: the month so far against the same number of
        // days last month. Comparing day 1-10 against a full 31-day month
        // guaranteed a "spending is 68% lower" insight on every single month.
        var daysElapsed = today.Day;
        var prevMonthComparableEnd = prevMonthStart.AddDays(daysElapsed - 1);

        var currentMonthExpenses = await _transactionRepository.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, startOfMonth, today, cancellationToken);
        var prevMonthExpenses = await _transactionRepository.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, prevMonthStart, prevMonthComparableEnd, cancellationToken);

        var currentTotal = currentMonthExpenses.Where(e => !e.IsDeleted).Sum(e => e.Amount.Amount);
        var prevTotal = prevMonthExpenses.Where(e => !e.IsDeleted).Sum(e => e.Amount.Amount);

        if (prevTotal > 0)
        {
            var changePercent = Math.Round(((currentTotal - prevTotal) / prevTotal) * 100, 2);
            if (changePercent > 10)
            {
                insights.Add(new SmartInsightDto(
                    "SpendingIncrease",
                    "Spending Increased",
                    $"You have spent {changePercent}% more so far this month than over the same days last month.",
                    InsightSeverity.Warning,
                    null,
                    null));
            }
            else if (changePercent < -10)
            {
                insights.Add(new SmartInsightDto(
                    "SpendingDecrease",
                    "Spending Decreased",
                    $"Nice work - you have spent {Math.Abs(changePercent)}% less so far this month than over the same days last month.",
                    InsightSeverity.Info,
                    null,
                    null));
            }
        }

        var categorySpending = currentMonthExpenses
            .Where(e => !e.IsDeleted)
            .GroupBy(e => e.CategoryId.Value)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(e => e.Amount.Amount) })
            .OrderByDescending(x => x.Total)
            .ToList();

        if (categorySpending.Count > 0)
        {
            var topCategory = categorySpending[0];
            var topCategoryEntity = await _categoryRepository.GetByIdAsync(topCategory.CategoryId, cancellationToken);
            var topCategoryName = topCategoryEntity?.Name ?? "Unknown";

            var prevCategoryTotal = prevMonthExpenses
                .Where(e => !e.IsDeleted && e.CategoryId.Value == topCategory.CategoryId)
                .Sum(e => e.Amount.Amount);

            if (prevCategoryTotal > 0)
            {
                var catChangePercent = Math.Round(((topCategory.Total - prevCategoryTotal) / prevCategoryTotal) * 100, 2);
                if (catChangePercent > 20)
                {
                    insights.Add(new SmartInsightDto(
                        "CategoryIncrease",
                        $"{topCategoryName} Spending Increased",
                        $"{topCategoryName} spending is {catChangePercent}% higher than over the same days last month.",
                        InsightSeverity.Warning,
                        topCategory.CategoryId,
                        "Category"));
                }
            }
        }

        var savingsRate = await CalculateSavingsRateAsync(userId, startOfMonth, today, cancellationToken);
        var prevSavingsRate = await CalculateSavingsRateAsync(userId, prevMonthStart, prevMonthComparableEnd, cancellationToken);

        if (prevSavingsRate > 0 && savingsRate > prevSavingsRate)
        {
            insights.Add(new SmartInsightDto(
                "SavingsImproved",
                "Savings Rate Improved",
                $"Your savings rate improved from {prevSavingsRate}% to {savingsRate}%.",
                InsightSeverity.Info,
                null,
                null));
        }

        var activeBudgets = await _budgetService.GetActiveAsync(userId, today, cancellationToken);
        foreach (var budget in activeBudgets)
        {
            if (!budget.IsNearLimit)
                continue;

            insights.Add(new SmartInsightDto(
                "BudgetNearLimit",
                $"{budget.Name} Near Limit",
                $"{budget.Name} is at {budget.PercentageUsed:F0}% of its {budget.Amount.Amount:N0} limit.",
                InsightSeverity.Critical,
                budget.Id,
                "Budget"));
        }

        return insights;
    }

    public async Task<IReadOnlyList<AnomalyDetectionDto>> DetectAnomaliesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var startDate = today.AddMonths(-3);
        var anomalies = new List<AnomalyDetectionDto>();

        var expenses = await _transactionRepository.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, startDate, today, cancellationToken);

        var activeExpenses = expenses.Where(e => !e.IsDeleted).ToList();
        var categoryStats = activeExpenses
            .GroupBy(e => e.CategoryId.Value)
            .Select(g =>
            {
                var amounts = g.Select(e => e.Amount.Amount).ToList();
                return new
                {
                    CategoryId = g.Key,
                    Count = amounts.Count,
                    Avg = amounts.Average(),
                    StdDev = CalculateStdDev(amounts)
                };
            })
            .Where(s => s.Count >= 5 && s.Avg > 0 && s.StdDev > 0)
            .ToList();

        foreach (var stat in categoryStats)
        {
            var category = await _categoryRepository.GetByIdAsync(stat.CategoryId, cancellationToken);
            var categoryName = category?.Name ?? "expense";
            var threshold = stat.Avg + (2 * stat.StdDev);

            var anomalousTransactions = activeExpenses
                .Where(e => e.CategoryId.Value == stat.CategoryId && e.Amount.Amount > threshold)
                .OrderByDescending(e => e.Amount.Amount)
                .Take(3);

            foreach (var tx in anomalousTransactions)
            {
                // stat.Avg is guarded above; keep the check local too so the
                // decimal division can never throw if the filter ever changes.
                if (stat.Avg <= 0)
                    continue;

                var deviation = Math.Round(((tx.Amount.Amount - stat.Avg) / stat.Avg) * 100, 2);
                anomalies.Add(new AnomalyDetectionDto(
                    tx.Id,
                    tx.Date,
                    tx.Amount,
                    new CategoryId(tx.CategoryId.Value),
                    categoryName,
                    new Money(Math.Max(0, stat.Avg - 2 * stat.StdDev), "PHP"),
                    new Money(threshold, "PHP"),
                    deviation,
                    $"This {categoryName} of {tx.Amount.Amount:N0} is {deviation:0}% higher than your usual {categoryName} spend."));
            }
        }

        return anomalies.OrderByDescending(a => a.DeviationPercentage).ToList();
    }

    private bool HasSufficientData(List<Transaction> expenses) =>
        MonthlyTotals(expenses).Count >= MinMonthsForPrediction &&
        expenses.Count >= MinTransactionsForPrediction;

    /// <summary>
    /// Month key to total spend, as an explicitly ordered list, oldest month
    /// first.
    /// <para>
    /// The ordering matters: the forecast weights the newest months most
    /// heavily and the trend comparison splits "recent" from "older" off this
    /// list. Transaction repositories return rows newest-first, so this has to
    /// sort explicitly - otherwise "recent" silently meant "oldest" and every
    /// trend came out backwards. A list rather than a dictionary because
    /// <c>Dictionary</c> enumeration order is not a documented contract.
    /// </para>
    /// </summary>
    private static List<(DateTime Month, decimal Total)> MonthlyTotals(List<Transaction> transactions) =>
        transactions
            .GroupBy(t => new DateTime(t.Date.Year, t.Date.Month, 1))
            .Select(g => (Month: g.Key, Total: g.Sum(t => t.Amount.Amount)))
            .OrderBy(x => x.Month)
            .ToList();

    /// <summary>
    /// Weighted moving average over the most recent months with a one standard
    /// deviation band. Expects <paramref name="monthlyTotals"/> oldest-first.
    /// </summary>
    private (Money predicted, Money min, Money max) CalculateCategoryPrediction(
        List<(DateTime Month, decimal Total)> monthlyTotals)
    {
        var values = monthlyTotals.Select(x => x.Total).ToList();
        var recentValues = values.TakeLast(3).ToList();

        var predicted = recentValues.Count >= 3
            ? recentValues.Zip(new[] { 0.5m, 0.3m, 0.2m }, (v, w) => v * w).Sum()
            : values.Average();

        var stdDev = CalculateStdDev(values);

        return (
            new Money(Math.Round(predicted, 2), "PHP"),
            new Money(Math.Round(Math.Max(0, predicted - stdDev), 2), "PHP"),
            new Money(Math.Round(predicted + stdDev, 2), "PHP"));
    }

    private static decimal CalculateStdDev(List<decimal> values)
    {
        if (values.Count < 2) return 0;
        var avg = values.Average();
        var variance = values.Sum(v => (double)(v - avg) * (double)(v - avg)) / (values.Count - 1);
        return (decimal)Math.Sqrt(variance);
    }

    private PredictionConfidence CalculateConfidence(
        int monthsAnalyzed,
        int transactionCount,
        List<(DateTime Month, decimal Total)> monthlyTotals)
    {
        if (monthsAnalyzed < MinMonthsForPrediction || transactionCount < MinTransactionsForPrediction)
            return PredictionConfidence.InsufficientData;

        var values = monthlyTotals.Select(x => x.Total).ToList();
        var average = values.Average();
        var cv = average > 0 ? CalculateStdDev(values) / average : 1;

        if (monthsAnalyzed >= 6 && transactionCount >= 50 && cv < 0.3m)
            return PredictionConfidence.High;
        if (monthsAnalyzed >= 3 && transactionCount >= 20 && cv < 0.5m)
            return PredictionConfidence.Moderate;

        return PredictionConfidence.Low;
    }

    /// <summary>
    /// Monthly-equivalent recurring total per category, fetched once per run
    /// instead of once per category.
    /// </summary>
    private async Task<Dictionary<Guid, Money>> GetRecurringMonthlyTotalsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var recurring = await _recurringRepository.GetActiveByUserIdAsync(userId, cancellationToken);

        return recurring
            .Where(r => r.Type == TransactionType.Expense && !r.EndDate.HasValue)
            .GroupBy(r => r.CategoryId.Value)
            .ToDictionary(
                g => g.Key,
                g => new Money(Math.Round(g.Sum(r => GetMonthlyEquivalent(r.Amount, r.Frequency)), 2), "PHP"));
    }

    private static decimal GetMonthlyEquivalent(Money amount, RecurringFrequency frequency) => frequency switch
    {
        RecurringFrequency.Daily => amount.Amount * 30,
        RecurringFrequency.Weekly => amount.Amount * 4.33m,
        RecurringFrequency.Monthly => amount.Amount,
        RecurringFrequency.Yearly => amount.Amount / 12,
        _ => amount.Amount
    };

    private async Task<decimal> CalculateSavingsRateAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        var income = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Income, startDate, endDate, cancellationToken);
        var expense = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Expense, startDate, endDate, cancellationToken);

        if (income.Amount == 0) return 0;

        return Math.Round(((income.Amount - expense.Amount) / income.Amount) * 100, 2);
    }
}