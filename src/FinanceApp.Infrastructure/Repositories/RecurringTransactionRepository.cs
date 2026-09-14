namespace FinanceApp.Infrastructure.Repositories;

using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class RecurringTransactionRepository : BaseRepository<RecurringTransaction>, IRecurringTransactionRepository
{
    public RecurringTransactionRepository(FinanceAppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<RecurringTransaction>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringTransaction>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(r => r.UserId == userId && r.IsActive)
            .OrderBy(r => r.NextDueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringTransaction>> GetDueTransactionsAsync(Guid userId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var date = asOfDate.Date;
        return await DbSet
            .Where(r => r.UserId == userId && r.IsActive && r.NextDueDate.HasValue && r.NextDueDate.Value.Date <= date)
            .OrderBy(r => r.NextDueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringTransaction>> GetPendingSyncAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(r => r.UserId == userId && r.SyncStatus != SyncStatus.Synced)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}