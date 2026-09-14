namespace FinanceApp.Infrastructure.Repositories;

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
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}