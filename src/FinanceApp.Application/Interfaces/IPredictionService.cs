namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.DTOs;

public interface IPredictionService
{
    Task<PredictionResultDto> GeneratePredictionAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ExpensePredictionDto> PredictExpensesAsync(Guid userId, int monthsAhead, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SpendingTrendDto>> AnalyzeTrendsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BudgetForecastDto>> ForecastBudgetsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SmartInsightDto>> GenerateInsightsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnomalyDetectionDto>> DetectAnomaliesAsync(Guid userId, CancellationToken cancellationToken = default);
}