namespace FinanceApp.Infrastructure.Repositories;

using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class TransactionRepository : BaseRepository<Transaction>, ITransactionRepository
{
    public TransactionRepository(FinanceAppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Transaction>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid userId, AccountId accountId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(t => t.UserId == userId && t.AccountId == accountId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetByCategoryIdAsync(Guid userId, CategoryId categoryId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(t => t.UserId == userId && t.CategoryId == categoryId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(t => t.UserId == userId && t.Date >= startDate.Date && t.Date <= endDate.Date)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetByTypeAsync(Guid userId, TransactionType type, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(t => t.UserId == userId && t.Type == type)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetByTypeAndDateRangeAsync(Guid userId, TransactionType type, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(t => t.UserId == userId && t.Type == type && t.Date >= startDate.Date && t.Date <= endDate.Date)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Money> GetTotalByTypeAsync(Guid userId, TransactionType type, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var total = await DbSet
            .Where(t => t.UserId == userId && t.Type == type && t.Date >= startDate.Date && t.Date <= endDate.Date)
            .SumAsync(t => t.Amount.Amount, cancellationToken);

        return new Money(total, "PHP");
    }

    public async Task<Money> GetTotalByCategoryAsync(Guid userId, CategoryId categoryId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var total = await DbSet
            .Where(t => t.UserId == userId && t.CategoryId == categoryId && t.Date >= startDate.Date && t.Date <= endDate.Date)
            .SumAsync(t => t.Amount.Amount, cancellationToken);

        return new Money(total, "PHP");
    }

    public async Task<IReadOnlyList<Transaction>> GetPendingSyncAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(t => t.UserId == userId && t.SyncStatus != SyncStatus.Synced)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetRecentAsync(Guid userId, int count, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetPagedAsync(
        Guid userId,
        TransactionQuery query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var ordered = ApplyFilter(DbSet.AsNoTracking().Where(t => t.UserId == userId), query)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt);

        return await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Filter precedence is intentionally identical to the branch order the
    /// service used when each combination was its own query, so moving paging
    /// into SQL does not quietly change which rows a filter combination returns.
    /// </summary>
    private static IQueryable<Transaction> ApplyFilter(IQueryable<Transaction> source, TransactionQuery query)
    {
        if (query.StartDate.HasValue && query.EndDate.HasValue && query.Type.HasValue)
        {
            return source.Where(t => t.Type == query.Type
                                    && t.Date >= query.StartDate.Value.Date
                                    && t.Date <= query.EndDate.Value.Date);
        }

        if (query.StartDate.HasValue && query.EndDate.HasValue)
        {
            return source.Where(t => t.Date >= query.StartDate.Value.Date && t.Date <= query.EndDate.Value.Date);
        }

        if (query.Type.HasValue)
            return source.Where(t => t.Type == query.Type);

        if (query.AccountId.HasValue)
            return source.Where(t => t.AccountId == query.AccountId.Value);

        if (query.CategoryId.HasValue)
            return source.Where(t => t.CategoryId == query.CategoryId.Value);

        return source;
    }

    public async Task<IReadOnlyList<CategoryTotal>> GetCategoryTotalsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await GetCategoryTotalsAsync(
            userId,
            Array.Empty<CategoryId>(),
            startDate,
            endDate,
            includeAllCategories: true,
            cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryTotal>> GetCategoryTotalsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> categoryIds,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default) =>
        await GetCategoryTotalsAsync(
            userId,
            categoryIds.Select(id => (CategoryId)id).ToList(),
            startDate,
            endDate,
            includeAllCategories: false,
            cancellationToken);

    /// <summary>
    /// Projects (CategoryId, Amount) for the window and buckets it here rather
    /// than grouping in SQL.
    /// <para>
    /// SQLite cannot express a month bucket through LINQ - <c>Date.Year</c> and
    /// <c>Date.Month</c> do not translate - and hand-writing strftime() over the
    /// column would couple this to EF's date storage format. The win being bought
    /// here is one round-trip and a two-column projection instead of N queries
    /// over fully materialised entities, so grouping a small result set is not
    /// where the time goes.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<CategoryTotal>> GetCategoryTotalsAsync(
        Guid userId,
        IReadOnlyCollection<CategoryId> categoryIds,
        DateTime startDate,
        DateTime endDate,
        bool includeAllCategories,
        CancellationToken cancellationToken)
    {
        var query = DbSet.AsNoTracking()
            .Where(t => t.UserId == userId
                        && t.Type == TransactionType.Expense
                        && t.Date >= startDate.Date
                        && t.Date <= endDate.Date);

        if (!includeAllCategories)
        {
            if (categoryIds.Count == 0)
                return Array.Empty<CategoryTotal>();

            // Contains against the value object, not CategoryId.Value: EF only
            // translates the former, because that is the property carrying the
            // conversion to the stored Guid.
            query = query.Where(t => categoryIds.Contains(t.CategoryId));
        }

        var rows = await query
            .Select(t => new { CategoryId = t.CategoryId.Value, Amount = t.Amount.Amount })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.CategoryId)
            .Select(g => new CategoryTotal(g.Key, g.Sum(r => r.Amount)))
            .ToList();
    }

    public async Task<IReadOnlyList<MonthlyTotal>> GetMonthlyTotalsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await DbSet.AsNoTracking()
            .Where(t => t.UserId == userId && t.Date >= startDate.Date && t.Date <= endDate.Date)
            .Select(t => new { t.Date, t.Type, Amount = t.Amount.Amount })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new { r.Date.Year, r.Date.Month, r.Type })
            .Select(g => new MonthlyTotal(g.Key.Year, g.Key.Month, g.Key.Type, g.Sum(r => r.Amount)))
            .ToList();
    }

    public async Task<int> CountByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking()
            .CountAsync(t => t.UserId == userId && t.Date >= startDate.Date && t.Date <= endDate.Date, cancellationToken);

    public async Task<IReadOnlyList<AccountNetAmount>> GetNetAmountsByAccountAsync(
        Guid userId,
        AccountId? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking().Where(t => t.UserId == userId);
        if (accountId.HasValue)
        {
            var wanted = accountId.Value;
            query = query.Where(t => t.AccountId == wanted);
        }

        // Grouped by currency as well as account: an account's balance is only
        // the sum of movements in the currency it is held in, and Money refuses
        // to add across currencies.
        var rows = await query
            .Select(t => new { t.AccountId, t.Type, Amount = t.Amount.Amount, Currency = t.Amount.Currency })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new { AccountId = r.AccountId.Value, r.Currency })
            .Select(g => new AccountNetAmount(
                g.Key.AccountId,
                g.Sum(r => r.Type == TransactionType.Income ? r.Amount : -r.Amount),
                g.Key.Currency))
            .ToList();
    }
}