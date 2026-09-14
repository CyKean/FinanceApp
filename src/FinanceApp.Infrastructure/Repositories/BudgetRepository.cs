namespace FinanceApp.Infrastructure.Repositories;

using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class BudgetRepository : BaseRepository<Budget>, IBudgetRepository
{
    public BudgetRepository(FinanceAppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Budget>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Budget>> GetActiveByUserIdAsync(Guid userId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var date = asOfDate.Date;
        return await DbSet
            .Where(b => b.UserId == userId && b.StartDate <= date && b.EndDate >= date)
            .OrderBy(b => b.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Budget>> GetByCategoryIdAsync(Guid userId, CategoryId categoryId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(b => b.UserId == userId && b.CategoryId == categoryId)
            .OrderByDescending(b => b.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Budget?> GetActiveForCategoryAsync(Guid userId, CategoryId categoryId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var date = asOfDate.Date;
        return await DbSet
            .FirstOrDefaultAsync(b => b.UserId == userId && b.CategoryId == categoryId && b.StartDate <= date && b.EndDate >= date, cancellationToken);
    }

    public async Task<IReadOnlyList<Budget>> GetPendingSyncAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(b => b.UserId == userId && b.SyncStatus != SyncStatus.Synced)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}