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
    private readonly IBudgetRepository _budgetRepository;
    private readonly IRecurringTransactionRepository _recurringRepository;
    private readonly ILogger<PredictionService> _logger;

    private const int MinMonthsForPrediction = 2;
    private const int MinTransactionsForPrediction = 10;

    public PredictionService(
        IUnitOfWork unitOfWork,
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IBudgetRepository budgetRepository,
        IRecurringTransactionRepository recurringRepository,
        ILogger<PredictionService> logger) : base(unitOfWork, logger)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _budgetRepository = budgetRepository;
        _recurringRepository = recurringRepository;
        _logger = logger;
    }

    public async Task<PredictionResultDto> GeneratePredictionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var expensePrediction = await PredictExpensesAsync(userId, 1, cancellationToken);
        var trends = await AnalyzeTrendsAsync(userId, cancellationToken);
        var budgetForecasts = await ForecastBudgetsAsync(userId, cancellationToken);
        var insights = await GenerateInsightsAsync(userId, cancellationToken);
        var anomalies = await DetectAnomaliesAsync(userId, cancellationToken);

        return new PredictionResultDto(
            expensePrediction,
            trends,
            budgetForecasts,
            insights,
            anomalies,
            DateTime.UtcNow);
    }

    public async Task<ExpensePredictionDto> PredictExpensesAsync(Guid userId, int monthsAhead, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddMonths(-12);

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

        var monthlyData = GroupByMonth(activeExpenses);
        var monthsAnalyzed = monthlyData.Count;
        var categoryPredictions = new List<CategoryPredictionDto>();

        var categories = await _categoryRepository.GetActiveByTypeAsync(userId, CategoryType.Expense, cancellationToken);
        var categoryDict = categories.ToDictionary(c => c.Id, c => c);

        foreach (var category in categories)
        {
            var catExpenses = activeExpenses.Where(e => e.CategoryId.Value == category.Id).ToList();
            if (!catExpenses.Any())
                continue;

            var catMonthlyData = GroupByMonth(catExpenses);
            var prediction = CalculateCategoryPrediction(catMonthlyData, catExpenses);
            var recurringAmount = await GetRecurringAmountForCategoryAsync(userId, new CategoryId(category.Id), cancellationToken);

            var finalPredicted = prediction.predicted.Add(recurringAmount);
            var minAmount = prediction.min.Add(recurringAmount);
            var maxAmount = prediction.max.Add(recurringAmount);

            categoryPredictions.Add(new CategoryPredictionDto(
                new CategoryId(category.Id),
                category.Name,
                category.Icon ?? "",
                category.Color ?? "",
                finalPredicted,
                minAmount,
                maxAmount));
        }

        var totalPredicted = categoryPredictions.Aggregate(Money.Zero("PHP"), (sum, cp) => sum.Add(cp.PredictedAmount));
        var minTotal = categoryPredictions.Aggregate(Money.Zero("PHP"), (sum, cp) => sum.Add(cp.MinAmount ?? Money.Zero("PHP")));
        var maxTotal = categoryPredictions.Aggregate(Money.Zero("PHP"), (sum, cp) => sum.Add(cp.MaxAmount ?? Money.Zero("PHP")));

        var confidence = CalculateConfidence(monthsAnalyzed, activeExpenses.Count, monthlyData);

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
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddMonths(-6);

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

            var monthlyData = GroupByMonth(catExpenses);
            if (monthlyData.Count < 3)
                continue;

            var recentMonths = monthlyData.TakeLast(3).ToList();
            var olderMonths = monthlyData.Take(monthlyData.Count - 3).ToList();

            var recentAvg = recentMonths.Any() ? recentMonths.Average(m => m.Value) : 0;
            var olderAvg = olderMonths.Any() ? olderMonths.Average(m => m.Value) : 0;

            if (olderAvg == 0)
                continue;

            var changePercent = Math.Round(((recentAvg - olderAvg) / olderAvg) * 100, 2);
            var trend = changePercent > 5 ? SpendingTrend.Increasing :
                       changePercent < -5 ? SpendingTrend.Decreasing :
                       SpendingTrend.Stable;

            trends.Add(new SpendingTrendDto(
                new CategoryId(category.Id),
                category.Name,
                trend,
                changePercent,
                new Money(recentAvg, "PHP"),
                new Money(olderAvg, "PHP")));
        }

        return trends.OrderByDescending(t => Math.Abs(t.ChangePercentage)).ToList();
    }

    public async Task<IReadOnlyList<BudgetForecastDto>> ForecastBudgetsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var activeBudgets = await _budgetRepository.GetActiveByUserIdAsync(userId, today, cancellationToken);
        var forecasts = new List<BudgetForecastDto>();

        var expensePrediction = await PredictExpensesAsync(userId, 1, cancellationToken);
        var categoryPredictions = expensePrediction.CategoryPredictions.ToDictionary(cp => cp.CategoryId, cp => cp);

        foreach (var budget in activeBudgets)
        {
            if (!categoryPredictions.TryGetValue(budget.CategoryId, out var prediction))
                continue;

            var predictedSpent = budget.SpentAmount.Add(prediction.PredictedAmount);
            var predictedRemaining = budget.Amount.Subtract(predictedSpent);
            var willExceed = predictedSpent > budget.Amount;
            var exceedPercent = budget.Amount.Amount > 0
                ? Math.Round(((predictedSpent.Amount - budget.Amount.Amount) / budget.Amount.Amount) * 100, 2)
                : 0;

            forecasts.Add(new BudgetForecastDto(
                budget.Id,
                budget.Name,
                budget.CategoryId,
                await GetCategoryNameAsync(budget.CategoryId, cancellationToken),
                budget.Amount,
                budget.SpentAmount,
                predictedSpent,
                predictedRemaining,
                willExceed,
                exceedPercent));
        }

        return forecasts;
    }

    public async Task<IReadOnlyList<SmartInsightDto>> GenerateInsightsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var insights = new List<SmartInsightDto>();
        var today = DateTime.UtcNow.Date;
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
        var prevMonthStart = startOfMonth.AddMonths(-1);
        var prevMonthEnd = startOfMonth.AddDays(-1);

        var currentMonthExpenses = await _transactionRepository.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, startOfMonth, endOfMonth, cancellationToken);
        var prevMonthExpenses = await _transactionRepository.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, prevMonthStart, prevMonthEnd, cancellationToken);

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
                    $"Your spending this month is {changePercent}% higher than last month.",
                    InsightSeverity.Warning,
                    null,
                    null));
            }
            else if (changePercent < -10)
            {
                insights.Add(new SmartInsightDto(
                    "SpendingDecrease",
                    "Spending Decreased",
                    $"Great job! Your spending this month is {Math.Abs(changePercent)}% lower than last month.",
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

        if (categorySpending.Any())
        {
            var topCategory = categorySpending.First();
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
                        $"{topCategoryName} spending is {catChangePercent}% higher than last month.",
                        InsightSeverity.Warning,
                        topCategory.CategoryId,
                        "Category"));
                }
            }
        }

        var savingsRate = await CalculateSavingsRateAsync(userId, startOfMonth, endOfMonth, cancellationToken);
        var prevSavingsRate = await CalculateSavingsRateAsync(userId, prevMonthStart, prevMonthEnd, cancellationToken);

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

        var activeBudgets = await _budgetRepository.GetActiveByUserIdAsync(userId, today, cancellationToken);
        foreach (var budget in activeBudgets)
        {
            if (budget.IsNearLimit(90))
            {
                var category = await _categoryRepository.GetByIdAsync(budget.CategoryId.Value, cancellationToken);
                insights.Add(new SmartInsightDto(
                    "BudgetNearLimit",
                    "Budget Near Limit",
                    $"{category?.Name ?? "Budget"} is at {budget.GetPercentageUsed()}% of its limit.",
                    InsightSeverity.Critical,
                    budget.Id,
                    "Budget"));
            }
        }

        return insights;
    }

    public async Task<IReadOnlyList<AnomalyDetectionDto>> DetectAnomaliesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddMonths(-3);
        var anomalies = new List<AnomalyDetectionDto>();

        var expenses = await _transactionRepository.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, startDate, today, cancellationToken);

        var activeExpenses = expenses.Where(e => !e.IsDeleted).ToList();
        var categoryStats = activeExpenses
            .GroupBy(e => e.CategoryId.Value)
            .Select(g => new
            {
                CategoryId = g.Key,
                Amounts = g.Select(e => e.Amount.Amount).ToList(),
                Avg = g.Average(e => e.Amount.Amount),
                StdDev = CalculateStdDev(g.Select(e => e.Amount.Amount).ToList())
            })
            .Where(s => s.Amounts.Count >= 5 && s.StdDev > 0)
            .ToList();

        foreach (var stat in categoryStats)
        {
            var category = await _categoryRepository.GetByIdAsync(stat.CategoryId, cancellationToken);
            var threshold = stat.Avg + (2 * stat.StdDev);

            var anomalousTransactions = activeExpenses
                .Where(e => e.CategoryId.Value == stat.CategoryId && e.Amount.Amount > threshold)
                .OrderByDescending(e => e.Amount.Amount)
                .Take(3);

            foreach (var tx in anomalousTransactions)
            {
                var deviation = Math.Round(((tx.Amount.Amount - stat.Avg) / stat.Avg) * 100, 2);
                anomalies.Add(new AnomalyDetectionDto(
                    tx.Id,
                    tx.Date,
                    tx.Amount,
                    new CategoryId(tx.CategoryId.Value),
                    category?.Name ?? "Unknown",
                    new Money(Math.Max(0, stat.Avg - 2 * stat.StdDev), "PHP"),
                    new Money(stat.Avg + 2 * stat.StdDev, "PHP"),
                    deviation,
                    $"This {category?.Name ?? "expense"} of {tx.Amount} is {deviation}% higher than your typical {category?.Name?.ToLower() ?? "expense"}."));
            }
        }

        return anomalies.OrderByDescending(a => a.DeviationPercentage).ToList();
    }

    private bool HasSufficientData(List<Transaction> expenses)
    {
        var monthlyData = GroupByMonth(expenses);
        return monthlyData.Count >= MinMonthsForPrediction && expenses.Count >= MinTransactionsForPrediction;
    }

    private Dictionary<DateTime, decimal> GroupByMonth(List<Transaction> transactions)
    {
        return transactions
            .GroupBy(t => new DateTime(t.Date.Year, t.Date.Month, 1))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(t => t.Amount.Amount));
    }

    private (Money predicted, Money min, Money max) CalculateCategoryPrediction(
        Dictionary<DateTime, decimal> monthlyData,
        List<Transaction> expenses)
    {
        var values = monthlyData.Values.ToList();
        var recentValues = values.TakeLast(3).ToList();

        decimal predicted;
        decimal min;
        decimal max;

        if (recentValues.Count >= 3)
        {
            var weights = new[] { 0.5m, 0.3m, 0.2m };
            predicted = recentValues.Zip(weights, (v, w) => v * w).Sum();
        }
        else
        {
            predicted = values.Average();
        }

        var stdDev = CalculateStdDev(values);
        min = Math.Max(0, predicted - stdDev);
        max = predicted + stdDev;

        return (
            new Money(Math.Round(predicted, 2), "PHP"),
            new Money(Math.Round(min, 2), "PHP"),
            new Money(Math.Round(max, 2), "PHP"));
    }

    private decimal CalculateStdDev(List<decimal> values)
    {
        if (values.Count < 2) return 0;
        var avg = values.Average();
        var variance = values.Sum(v => (double)(v - avg) * (double)(v - avg)) / (values.Count - 1);
        return (decimal)Math.Sqrt(variance);
    }

    private PredictionConfidence CalculateConfidence(int monthsAnalyzed, int transactionCount, Dictionary<DateTime, decimal> monthlyData)
    {
        if (monthsAnalyzed < MinMonthsForPrediction || transactionCount < MinTransactionsForPrediction)
            return PredictionConfidence.InsufficientData;

        var values = monthlyData.Values.ToList();
        var cv = values.Average() > 0 ? CalculateStdDev(values) / values.Average() : 1;

        if (monthsAnalyzed >= 6 && transactionCount >= 50 && cv < 0.3m)
            return PredictionConfidence.High;
        if (monthsAnalyzed >= 3 && transactionCount >= 20 && cv < 0.5m)
            return PredictionConfidence.Moderate;

        return PredictionConfidence.Low;
    }

    private async Task<Money> GetRecurringAmountForCategoryAsync(Guid userId, CategoryId categoryId, CancellationToken cancellationToken)
    {
        var recurring = await _recurringRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        var catRecurring = recurring.Where(r => r.CategoryId == categoryId && r.Type == TransactionType.Expense).ToList();

        if (!catRecurring.Any())
            return Money.Zero("PHP");

        var monthlyTotal = catRecurring.Sum(r => GetMonthlyEquivalent(r.Amount, r.Frequency));
        return new Money(Math.Round(monthlyTotal, 2), "PHP");
    }

    private decimal GetMonthlyEquivalent(Money amount, RecurringFrequency frequency)
    {
        return frequency switch
        {
            RecurringFrequency.Daily => amount.Amount * 30,
            RecurringFrequency.Weekly => amount.Amount * 4.33m,
            RecurringFrequency.Monthly => amount.Amount,
            RecurringFrequency.Yearly => amount.Amount / 12,
            _ => amount.Amount
        };
    }

    private async Task<string> GetCategoryNameAsync(CategoryId categoryId, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId.Value, cancellationToken);
        return category?.Name ?? "Unknown";
    }

    private async Task<decimal> CalculateSavingsRateAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        var income = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Income, startDate, endDate, cancellationToken);
        var expense = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Expense, startDate, endDate, cancellationToken);

        if (income.Amount == 0) return 0;

        var net = income.Amount - expense.Amount;
        return Math.Round((net / income.Amount) * 100, 2);
    }
}