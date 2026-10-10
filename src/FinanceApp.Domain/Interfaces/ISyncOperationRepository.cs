namespace FinanceApp.Domain.Interfaces;

using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;

public interface ISyncOperationRepository : IRepository<SyncOperation>
{
    Task<IReadOnlyList<SyncOperation>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncOperation>> GetFailedByUserIdAsync(Guid userId, int maxRetries, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncOperation>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every (EntityType, EntityId) pair already recorded for a user, in one
    /// query. Healing the outbox used to ask the database once per entity, which
    /// on a few thousand transactions meant a few thousand round-trips a minute.
    /// </summary>
    Task<IReadOnlySet<(string EntityType, Guid EntityId)>> GetTrackedEntitiesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rows a user owns that have no outbox entry at all, as (EntityType, Id,
    /// SyncStatus). This is the anti-join that replaced loading every entity to
    /// diff it against the outbox in memory: six indexed scans that return only
    /// the rows that actually need queueing, rather than a full materialisation
    /// of every table the user has ever written.
    /// <para>
    /// Normally empty. It is not empty for a database written before the outbox
    /// existed, or one whose rows were copied in by a seeding or identity
    /// migration, which is what the heal in SyncService exists to repair.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<(string EntityType, Guid EntityId, SyncStatus SyncStatus)>> GetUntrackedEntitiesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

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