namespace FinanceApp.Application.DTOs;

using FinanceApp.Domain.ValueObjects;

/// <summary>
/// One authoritative Statistics result, produced by DashboardService and
/// consumed by every Statistics UI component. Nothing in the UI computes
/// financial figures of its own.
/// </summary>
public record StatisticsDto(
    Money TotalBalance,
    Money TotalSpending,
    Money TotalEarning,
    Money NetAmount,
    decimal SavingsRate,
    decimal? SpendingChangePercent,
    decimal? EarningChangePercent,
    IReadOnlyList<StatisticsChartPointDto> Overview,
    IReadOnlyList<SpendingSliceDto> SpendingByAccountType,
    FinancialGoalDto? PrimaryGoal);

/// <summary>One bar-group of the Overview chart: spend and earnings for a bucket.</summary>
public record StatisticsChartPointDto(
    string Label,
    DateTime Start,
    DateTime End,
    Money Spending,
    Money Earning);

/// <summary>Spending share for one account type, for the Spending breakdown chart.</summary>
public record SpendingSliceDto(
    string Label,
    Money Amount,
    decimal Percentage);
