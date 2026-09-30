namespace FinanceApp.Infrastructure.Repositories;

using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class SyncOperationRepository : BaseRepository<SyncOperation>, ISyncOperationRepository
{
    public SyncOperationRepository(FinanceAppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<SyncOperation>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.UserId == userId && s.Status != SyncStatus.Synced)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SyncOperation>> GetFailedByUserIdAsync(Guid userId, int maxRetries, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.UserId == userId && s.Status == SyncStatus.Failed && s.RetryCount < maxRetries)
            .OrderBy(s => s.LastAttemptAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SyncOperation>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.EntityType == entityType && s.EntityId == entityId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetPendingCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .CountAsync(s => s.UserId == userId && s.Status != SyncStatus.Synced, cancellationToken);
    }

    public async Task<IReadOnlyList<SyncOperation>> GetRecentByUserIdAsync(Guid userId, int count, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<DateTime?> GetLastSyncedAtAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.UserId == userId && s.Status == SyncStatus.Synced)
            .OrderByDescending(s => s.LastAttemptAt)
            .Select(s => s.LastAttemptAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}