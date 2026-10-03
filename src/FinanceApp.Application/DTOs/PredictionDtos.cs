namespace FinanceApp.Application.DTOs;

using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public record CategoryPredictionDto(
    CategoryId CategoryId,
    string CategoryName,
    string CategoryIcon,
    string CategoryColor,
    Money PredictedAmount,
    Money? MinAmount,
    Money? MaxAmount
);

public record ExpensePredictionDto(
    Money TotalPredicted,
    Money? MinTotal,
    Money? MaxTotal,
    PredictionConfidence Confidence,
    IReadOnlyList<CategoryPredictionDto> CategoryPredictions,
    DateTime PredictionDate,
    int MonthsAnalyzed,
    int TotalTransactionsAnalyzed
);

public record SpendingTrendDto(
    CategoryId CategoryId,
    string CategoryName,
    string CategoryIcon,
    SpendingTrend Trend,
    decimal ChangePercentage,
    Money CurrentAverage,
    Money PreviousAverage
);

public record BudgetForecastDto(
    Guid BudgetId,
    string BudgetName,
    CategoryId CategoryId,
    string CategoryName,
    string CategoryIcon,
    string CategoryColor,
    Money BudgetAmount,
    Money CurrentSpent,
    Money PredictedSpent,
    Money PredictedRemaining,
    decimal PercentageUsed,
    bool WillExceedBudget,
    decimal ExceedPercentage
);

public record SmartInsightDto(
    string Type,
    string Title,
    string Message,
    InsightSeverity Severity,
    Guid? RelatedEntityId,
    string? RelatedEntityType
);

public record AnomalyDetectionDto(
    Guid TransactionId,
    DateTime TransactionDate,
    Money Amount,
    CategoryId CategoryId,
    string CategoryName,
    Money TypicalMin,
    Money TypicalMax,
    decimal DeviationPercentage,
    string Message
);

public enum InsightSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public record PredictionResultDto(
    ExpensePredictionDto? ExpensePrediction,
    IReadOnlyList<SpendingTrendDto> SpendingTrends,
    IReadOnlyList<BudgetForecastDto> BudgetForecasts,
    IReadOnlyList<SmartInsightDto> Insights,
    IReadOnlyList<AnomalyDetectionDto> Anomalies,
    DateTime GeneratedAt
);