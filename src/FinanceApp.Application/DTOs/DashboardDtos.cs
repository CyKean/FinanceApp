namespace FinanceApp.Application.DTOs;

using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public record DashboardDto(
    Money TotalBalance,
    Money TotalIncome,
    Money TotalExpense,
    Money NetAmount,
    decimal SavingsRate,
    IReadOnlyList<TransactionDto> RecentTransactions,
    IReadOnlyList<CategorySpendingDto> SpendingByCategory,
    IReadOnlyList<MonthlyTrendDto> MonthlyTrends,
    IReadOnlyList<BudgetDto> ActiveBudgets,
    IReadOnlyList<FinancialGoalDto> ActiveGoals
);

public record CategorySpendingDto(
    CategoryId CategoryId,
    string CategoryName,
    string CategoryIcon,
    string CategoryColor,
    Money Amount,
    decimal Percentage
);

public record MonthlyTrendDto(
    int Year,
    int Month,
    Money Income,
    Money Expense,
    Money Net
)
{
    public DateTime Date => new(Year, Month, 1);
}

public record CalendarEventDto(
    DateTime Date,
    TransactionType Type,
    Money Amount,
    string CategoryName,
    string CategoryIcon,
    string CategoryColor,
    string? Notes
);

public record AnalyticsDto(
    Money MonthlyIncome,
    Money MonthlyExpense,
    Money IncomeVsExpense,
    IReadOnlyList<CategorySpendingDto> CategoryBreakdown,
    IReadOnlyList<MonthlyTrendDto> SpendingTrends,
    decimal SavingsRate,
    Money AverageDailySpending,
    Money AverageMonthlySpending,
    IReadOnlyList<CategorySpendingDto> HighestSpendingCategories,
    int DaysInPeriod,
    decimal MonthsInPeriod);