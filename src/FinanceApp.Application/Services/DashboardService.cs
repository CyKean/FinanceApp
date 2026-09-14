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
        var today = DateTime.UtcNow.Date;
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
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddMonths(-months + 1).Date;

        var monthlyIncome = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Income, startDate, today, cancellationToken);
        var monthlyExpense = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Expense, startDate, today, cancellationToken);

        var spendingByCategory = await GetSpendingByCategoryAsync(userId, startDate, today, cancellationToken);
        var monthlyTrends = await GetMonthlyTrendsAsync(userId, months, cancellationToken);

        var savingsRate = monthlyIncome.Amount > 0
            ? Math.Round(((monthlyIncome.Amount - monthlyExpense.Amount) / monthlyIncome.Amount) * 100, 2)
            : 0;

        var daysInPeriod = (today - startDate).Days + 1;
        var averageDailySpending = daysInPeriod > 0
            ? new Money(Math.Round(monthlyExpense.Amount / daysInPeriod, 2), monthlyExpense.Currency)
            : Money.Zero(monthlyExpense.Currency);

        var averageMonthlySpending = months > 0
            ? new Money(Math.Round(monthlyExpense.Amount / months, 2), monthlyExpense.Currency)
            : Money.Zero(monthlyExpense.Currency);

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
            highestSpendingCategories);
    }

    public async Task<IReadOnlyList<CalendarEventDto>> GetCalendarEventsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var transactions = await _transactionRepository.GetByDateRangeAsync(userId, startDate, endDate, cancellationToken);
        var categoryIds = transactions.Select(t => t.CategoryId.Value).Distinct().ToList();

        var categories = new Dictionary<Guid, Category>();
        foreach (var catId in categoryIds)
        {
            var cat = await _categoryRepository.GetByIdAsync(catId, cancellationToken);
            if (cat != null) categories[catId] = cat;
        }

        return transactions
            .Where(t => !t.IsDeleted)
            .Select(t => new CalendarEventDto(
                t.Date,
                t.Type,
                t.Amount,
                categories.TryGetValue(t.CategoryId.Value, out var cat) ? cat.Name : "",
                categories.TryGetValue(t.CategoryId.Value, out cat) ? cat.Icon ?? "" : "",
                categories.TryGetValue(t.CategoryId.Value, out cat) ? cat.Color ?? "" : "",
                t.Notes))
            .OrderBy(e => e.Date)
            .ToList();
    }

    private async Task<IReadOnlyList<TransactionDto>> MapTransactionsToDto(
        IReadOnlyList<Transaction> transactions,
        CancellationToken cancellationToken)
    {
        var accountIds = transactions.Select(t => t.AccountId.Value).Distinct().ToList();
        var categoryIds = transactions.Select(t => t.CategoryId.Value).Distinct().ToList();

        var accounts = new Dictionary<Guid, Account>();
        foreach (var accId in accountIds)
        {
            var acc = await _accountRepository.GetByIdAsync(accId, cancellationToken);
            if (acc != null) accounts[accId] = acc;
        }

        var categories = new Dictionary<Guid, Category>();
        foreach (var catId in categoryIds)
        {
            var cat = await _categoryRepository.GetByIdAsync(catId, cancellationToken);
            if (cat != null) categories[catId] = cat;
        }

        return transactions.Select(t => t.ToDto(
            accounts.TryGetValue(t.AccountId.Value, out var acc) ? acc.Name : "",
            categories.TryGetValue(t.CategoryId.Value, out var cat) ? cat.Name : "",
            categories.TryGetValue(t.CategoryId.Value, out cat) ? cat.Icon ?? "" : "",
            categories.TryGetValue(t.CategoryId.Value, out cat) ? cat.Color ?? "" : ""
        )).ToList();
    }

    private async Task<IReadOnlyList<CategorySpendingDto>> GetSpendingByCategoryAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var expenses = await _transactionRepository.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, startDate, endDate, cancellationToken);

        var categoryTotals = expenses
            .Where(e => !e.IsDeleted)
            .GroupBy(e => e.CategoryId)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(e => e.Amount.Amount) })
            .OrderByDescending(x => x.Total)
            .ToList();

        var totalAmount = categoryTotals.Sum(x => x.Total);
        var categoryIds = categoryTotals.Select(x => x.CategoryId.Value).ToList();

        var categories = new Dictionary<Guid, Category>();
        foreach (var catId in categoryIds)
        {
            var cat = await _categoryRepository.GetByIdAsync(catId, cancellationToken);
            if (cat != null) categories[catId] = cat;
        }

        var currency = categoryTotals.FirstOrDefault()?.CategoryId != null
            ? (await _categoryRepository.GetByIdAsync(categoryTotals.First().CategoryId.Value, cancellationToken))?.Name != null
                ? "PHP"
                : "PHP"
            : "PHP";

        return categoryTotals.Select(x =>
        {
            var cat = categories.TryGetValue(x.CategoryId.Value, out var c) ? c : null;
            var percentage = totalAmount > 0 ? Math.Round((x.Total / totalAmount) * 100, 2) : 0;
            return new CategorySpendingDto(
                x.CategoryId,
                cat?.Name ?? "Unknown",
                cat?.Icon ?? "",
                cat?.Color ?? "",
                new Money(x.Total, currency),
                percentage);
        }).ToList();
    }

    private async Task<IReadOnlyList<MonthlyTrendDto>> GetMonthlyTrendsAsync(
        Guid userId,
        int months,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var results = new List<MonthlyTrendDto>();

        for (int i = months - 1; i >= 0; i--)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var income = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Income, monthStart, monthEnd, cancellationToken);
            var expense = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Expense, monthStart, monthEnd, cancellationToken);

            results.Add(new MonthlyTrendDto(
                monthStart.Year,
                monthStart.Month,
                income,
                expense,
                income.Subtract(expense)));
        }

        return results;
    }

    private async Task<IReadOnlyList<BudgetDto>> MapBudgetsToDto(
        IReadOnlyList<Budget> budgets,
        CancellationToken cancellationToken)
    {
        var categoryIds = budgets.Select(b => b.CategoryId.Value).Distinct().ToList();

        var categories = new Dictionary<Guid, Category>();
        foreach (var catId in categoryIds)
        {
            var cat = await _categoryRepository.GetByIdAsync(catId, cancellationToken);
            if (cat != null) categories[catId] = cat;
        }

        foreach (var budget in budgets)
        {
            var spent = await _transactionRepository.GetTotalByCategoryAsync(
                budget.UserId,
                budget.CategoryId,
                budget.StartDate,
                budget.EndDate,
                cancellationToken);

            budget.ResetSpending();
            budget.AddSpending(spent);
        }

        await UnitOfWork.SaveChangesAsync(cancellationToken);

        return budgets.Select(b => b.ToDto(
            categories.TryGetValue(b.CategoryId.Value, out var cat) ? cat.Name : "",
            categories.TryGetValue(b.CategoryId.Value, out cat) ? cat.Icon ?? "" : "",
            categories.TryGetValue(b.CategoryId.Value, out cat) ? cat.Color ?? "" : ""
        )).ToList();
    }

    private IReadOnlyList<FinancialGoalDto> MapGoalsToDto(IReadOnlyList<FinancialGoal> goals)
    {
        return goals.Select(g => g.ToDto()).ToList();
    }
}