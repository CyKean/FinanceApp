namespace FinanceApp.Application.Services;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Application.Mappings;
using Microsoft.Extensions.Logging;

public class DashboardService : BaseService, IDashboardService
{
    /// <summary>
    /// Every aggregate in the repository layer returns PHP, so the charts label
    /// themselves consistently instead of each caller guessing. This also
    /// replaces a dead query whose three branches all returned the same literal.
    /// </summary>
    private const string DefaultCurrency = "PHP";

    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IFinancialGoalRepository _goalRepository;

    public DashboardService(
        IUnitOfWork unitOfWork,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IBudgetRepository budgetRepository,
        IFinancialGoalRepository goalRepository,
        ILogger<DashboardService> logger) : base(unitOfWork, logger)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _budgetRepository = budgetRepository;
        _goalRepository = goalRepository;
    }

    public async Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var totalBalance = await _accountRepository.GetTotalBalanceAsync(userId, cancellationToken);

        // Local, not UTC: transactions are stored with the dates the user
        // entered. For a UTC+8 user, UtcNow.Date is yesterday between midnight
        // and 08:00, so on the 1st of a month the whole dashboard reported the
        // previous month.
        var today = DateTime.Today;
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

        var totalIncome = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Income, startOfMonth, endOfMonth, cancellationToken);
        var totalExpense = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Expense, startOfMonth, endOfMonth, cancellationToken);
        var netAmount = totalIncome.Subtract(totalExpense);

        var savingsRate = totalIncome.Amount > 0
            ? Math.Round((netAmount.Amount / totalIncome.Amount) * 100, 2)
            : 0;

        var recentTransactions = await _transactionRepository.GetRecentAsync(userId, 10, cancellationToken);
        var recentTxDtos = await MapTransactionsToDto(recentTransactions, cancellationToken);

        var spendingByCategory = await GetSpendingByCategoryAsync(userId, startOfMonth, endOfMonth, cancellationToken);
        var monthlyTrends = await GetMonthlyTrendsAsync(userId, 6, cancellationToken);
        var activeBudgets = await _budgetRepository.GetActiveByUserIdAsync(userId, today, cancellationToken);
        var activeBudgetsDto = await MapBudgetsToDto(activeBudgets, cancellationToken);
        var activeGoals = await _goalRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        var activeGoalsDto = MapGoalsToDto(activeGoals);

        return new DashboardDto(
            totalBalance,
            totalIncome,
            totalExpense,
            netAmount,
            savingsRate,
            recentTxDtos,
            spendingByCategory,
            monthlyTrends,
            activeBudgetsDto,
            activeGoalsDto);
    }

    public async Task<AnalyticsDto> GetAnalyticsAsync(Guid userId, int months, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var startDate = today.AddMonths(-months + 1).Date;

        var monthlyIncome = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Income, startDate, today, cancellationToken);
        var monthlyExpense = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Expense, startDate, today, cancellationToken);

        var spendingByCategory = await GetSpendingByCategoryAsync(userId, startDate, today, cancellationToken);
        var monthlyTrends = await GetMonthlyTrendsAsync(userId, months, cancellationToken);

        var savingsRate = monthlyIncome.Amount > 0
            ? Math.Round(((monthlyIncome.Amount - monthlyExpense.Amount) / monthlyIncome.Amount) * 100, 2)
            : 0;

        // The window spans whole calendar months, so it is rarely the requested
        // number of months: asking for 3 on the 3rd gives 62 days. Callers that
        // average per month have to divide by the elapsed span, not by "months".
        var daysInPeriod = Math.Max(1, (today - startDate).Days + 1);
        var monthsInPeriod = Math.Round(daysInPeriod / (365m / 12m), 4);

        var averageDailySpending = new Money(
            Math.Round(monthlyExpense.Amount / daysInPeriod, 2),
            monthlyExpense.Currency);

        var averageMonthlySpending = new Money(
            Math.Round(monthlyExpense.Amount / monthsInPeriod, 2),
            monthlyExpense.Currency);

        var highestSpendingCategories = spendingByCategory
            .OrderByDescending(c => c.Amount.Amount)
            .Take(5)
            .ToList();

        return new AnalyticsDto(
            monthlyIncome,
            monthlyExpense,
            monthlyIncome.Subtract(monthlyExpense),
            spendingByCategory,
            monthlyTrends,
            savingsRate,
            averageDailySpending,
            averageMonthlySpending,
            highestSpendingCategories,
            daysInPeriod,
            monthsInPeriod);
    }

    public async Task<IReadOnlyList<CalendarEventDto>> GetCalendarEventsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var transactions = await _transactionRepository.GetByDateRangeAsync(userId, startDate, endDate, cancellationToken);
        if (transactions.Count == 0)
            return Array.Empty<CalendarEventDto>();

        // One query for every category in the month rather than one per category.
        var categories = await _categoryRepository.GetByIdsAsync(
            transactions.Select(t => t.CategoryId.Value).Distinct().ToList(), cancellationToken);

        return transactions
            .Where(t => !t.IsDeleted)
            .Select(t =>
            {
                categories.TryGetValue(t.CategoryId.Value, out var cat);

                return new CalendarEventDto(
                    t.Date,
                    t.Type,
                    t.Amount,
                    cat?.Name ?? "",
                    cat?.Icon ?? "",
                    cat?.Color ?? "",
                    t.Notes);
            })
            .OrderBy(e => e.Date)
            .ToList();
    }

    private async Task<IReadOnlyList<TransactionDto>> MapTransactionsToDto(
        IReadOnlyList<Transaction> transactions,
        CancellationToken cancellationToken)
    {
        if (transactions.Count == 0)
            return Array.Empty<TransactionDto>();

        // Two queries for the whole list, not one per transaction. Ten recent
        // transactions used to cost twenty round-trips.
        var accounts = await _accountRepository.GetByIdsAsync(
            transactions.Select(t => t.AccountId.Value).Distinct().ToList(), cancellationToken);

        var categories = await _categoryRepository.GetByIdsAsync(
            transactions.Select(t => t.CategoryId.Value).Distinct().ToList(), cancellationToken);

        return transactions.Select(t =>
        {
            accounts.TryGetValue(t.AccountId.Value, out var acc);
            categories.TryGetValue(t.CategoryId.Value, out var cat);

            return t.ToDto(
                acc?.Name ?? "",
                cat?.Name ?? "",
                cat?.Icon ?? "",
                cat?.Color ?? "");
        }).ToList();
    }

    private async Task<IReadOnlyList<CategorySpendingDto>> GetSpendingByCategoryAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        // One aggregate instead of materialising every expense row in the window
        // just to group them.
        var categoryTotals = await _transactionRepository.GetCategoryTotalsAsync(userId, startDate, endDate, cancellationToken);
        if (categoryTotals.Count == 0)
            return Array.Empty<CategorySpendingDto>();

        var categories = await _categoryRepository.GetByIdsAsync(
            categoryTotals.Select(t => t.CategoryId).ToList(), cancellationToken);

        var totalAmount = categoryTotals.Sum(x => x.Total);

        return categoryTotals.Select(x =>
        {
            categories.TryGetValue(x.CategoryId, out var cat);
            var percentage = totalAmount > 0 ? Math.Round((x.Total / totalAmount) * 100, 2) : 0;

            return new CategorySpendingDto(
                new CategoryId(x.CategoryId),
                cat?.Name ?? "Unknown",
                cat?.Icon ?? "",
                cat?.Color ?? "",
                new Money(x.Total, DefaultCurrency),
                percentage);
        }).ToList();
    }

    private async Task<IReadOnlyList<MonthlyTrendDto>> GetMonthlyTrendsAsync(
        Guid userId,
        int months,
        CancellationToken cancellationToken)
    {
        // Local, not UTC, for the same reason GetDashboardAsync uses it: a UTC+8
        // user between midnight and 08:00 was being bucketed into the previous
        // month, which put the current month in the future and dropped a period.
        var today = DateTime.Today;
        var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-(months - 1));

        // One round-trip for the whole chart. It used to be two queries per month,
        // so a six-month trend was twelve.
        var totals = await _transactionRepository.GetMonthlyTotalsAsync(userId, firstMonth, today, cancellationToken);

        var byMonth = totals
            .GroupBy(t => (t.Year, t.Month))
            .ToDictionary(g => g.Key, g => g.ToDictionary(t => t.Type, t => t.Total));

        var results = new List<MonthlyTrendDto>(months);
        for (var i = 0; i < months; i++)
        {
            var month = firstMonth.AddMonths(i);

            byMonth.TryGetValue((month.Year, month.Month), out var forMonth);

            var income = forMonth is not null && forMonth.TryGetValue(TransactionType.Income, out var inTotal)
                ? new Money(inTotal, DefaultCurrency)
                : Money.Zero(DefaultCurrency);

            var expense = forMonth is not null && forMonth.TryGetValue(TransactionType.Expense, out var outTotal)
                ? new Money(outTotal, DefaultCurrency)
                : Money.Zero(DefaultCurrency);

            results.Add(new MonthlyTrendDto(month.Year, month.Month, income, expense, income.Subtract(expense)));
        }

        return results;
    }

    private async Task<IReadOnlyList<BudgetDto>> MapBudgetsToDto(
        IReadOnlyList<Budget> budgets,
        CancellationToken cancellationToken)
    {
        if (budgets.Count == 0)
            return Array.Empty<BudgetDto>();

        var categories = await _categoryRepository.GetByIdsAsync(
            budgets.Select(b => b.CategoryId.Value).Distinct().ToList(), cancellationToken);

        // Budgets usually share a window (the current month), so they are grouped
        // by window and answered with one aggregate each - N queries became 1.
        // Nothing is written back: spent is projected into the DTO instead, so a
        // dashboard visit no longer takes SQLite's write lock.
        var spentByCategory = new Dictionary<Guid, decimal>();

        foreach (var window in budgets
                     .GroupBy(b => (Start: b.StartDate.Date, End: b.EndDate.Date)))
        {
            var totals = await _transactionRepository.GetCategoryTotalsAsync(
                userId: budgets[0].UserId,
                categoryIds: window.Select(b => b.CategoryId.Value).ToList(),
                startDate: window.Key.Start,
                endDate: window.Key.End,
                cancellationToken);

            foreach (var total in totals)
            {
                spentByCategory[total.CategoryId] = total.Total;
            }
        }

        return budgets.Select(b =>
        {
            categories.TryGetValue(b.CategoryId.Value, out var cat);
            spentByCategory.TryGetValue(b.CategoryId.Value, out var spent);

            return b.ToDto(
                cat?.Name ?? "",
                cat?.Icon ?? "",
                cat?.Color ?? "",
                new Money(spent, b.Amount.Currency));
        }).ToList();
    }

    private IReadOnlyList<FinancialGoalDto> MapGoalsToDto(IReadOnlyList<FinancialGoal> goals)
    {
        return goals.Select(g => g.ToDto()).ToList();
    }
}