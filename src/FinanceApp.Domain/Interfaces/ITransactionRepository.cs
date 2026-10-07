namespace FinanceApp.Domain.Interfaces;

using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<IReadOnlyList<Transaction>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid userId, AccountId accountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByCategoryIdAsync(Guid userId, CategoryId categoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByTypeAsync(Guid userId, TransactionType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByTypeAndDateRangeAsync(Guid userId, TransactionType type, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<Money> GetTotalByTypeAsync(Guid userId, TransactionType type, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<Money> GetTotalByCategoryAsync(Guid userId, CategoryId categoryId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetPendingSyncAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetRecentAsync(Guid userId, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// One statement with LIMIT/OFFSET. Paging in the service meant page 1 read
    /// the user's entire history and threw most of it away.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetPagedAsync(Guid userId, TransactionQuery query, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Spend per category over a window, without materialising the rows.</summary>
    Task<IReadOnlyList<CategoryTotal>> GetCategoryTotalsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Spend per category restricted to the given categories. Lets a caller with
    /// N budgets run one query instead of N.
    /// </summary>
    Task<IReadOnlyList<CategoryTotal>> GetCategoryTotalsAsync(Guid userId, IReadOnlyCollection<Guid> categoryIds, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Income and expense per calendar month over a window, in one round-trip.</summary>
    Task<IReadOnlyList<MonthlyTotal>> GetMonthlyTotalsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Income and expense per calendar day over a window, in one round-trip.</summary>
    Task<IReadOnlyList<DailyTotal>> GetDailyTotalsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>Row count over a window, for a COUNT in SQL rather than in memory.</summary>
    Task<int> CountByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Net amount of every account's transactions (income positive, expense
    /// negative) in one query. This is what an account's running balance is
    /// derived from. Pass an account id to narrow it to one account, which
    /// matters on the single-account paths: without it, saving one transaction
    /// would read the user's entire history to add up one account.
    /// <para>
    /// One entry per account and currency, so the caller can pick the movements
    /// matching the currency an account is held in.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AccountNetAmount>> GetNetAmountsByAccountAsync(Guid userId, AccountId? accountId = null, CancellationToken cancellationToken = default);
}