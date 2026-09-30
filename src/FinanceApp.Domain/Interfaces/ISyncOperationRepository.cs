namespace FinanceApp.Domain.Interfaces;

using FinanceApp.Domain.Entities;

public interface ISyncOperationRepository : IRepository<SyncOperation>
{
    Task<IReadOnlyList<SyncOperation>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncOperation>> GetFailedByUserIdAsync(Guid userId, int maxRetries, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncOperation>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
    Task<int> GetPendingCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Most recent operations (any status) for sync history display.
    /// </summary>
    Task<IReadOnlyList<SyncOperation>> GetRecentByUserIdAsync(Guid userId, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// When the last operation completed successfully (null if never).
    /// </summary>
    Task<DateTime?> GetLastSyncedAtAsync(Guid userId, CancellationToken cancellationToken = default);
}