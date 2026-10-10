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

    public async Task<IReadOnlySet<(string EntityType, Guid EntityId)>> GetTrackedEntitiesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var pairs = await DbSet
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new { s.EntityType, s.EntityId })
            .ToListAsync(cancellationToken);

        return pairs
            .Select(p => (p.EntityType, p.EntityId))
            .ToHashSet();
    }

    /// <summary>
    /// The synced entity tables, in the order SyncService names them.
    /// </summary>
    private static readonly (string EntityType, string Table)[] s_syncableEntities =
    {
        ("Account", "Accounts"),
        ("Category", "Categories"),
        ("Transaction", "Transactions"),
        ("Budget", "Budgets"),
        ("RecurringTransaction", "RecurringTransactions"),
        ("FinancialGoal", "FinancialGoals")
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<(string EntityType, Guid EntityId, SyncStatus SyncStatus)>> GetUntrackedEntitiesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // One UNION ALL of six NOT EXISTS anti-joins. This replaces loading all
        // six tables in full, tracking every entity, and diffing them against the
        // outbox in memory - which is what the sync tick used to do every minute.
        // Each branch is served by the (UserId, ...) index and returns nothing
        // unless a row genuinely has no outbox entry, so the steady-state cost is
        // six index probes returning zero rows rather than one materialised copy
        // of the user's entire history.
        //
        // Written as SQL rather than LINQ because EF cannot translate a correlated
        // NOT EXISTS across six unrelated DbSets into a single round-trip.
        var union = string.Join(
            "\nUNION ALL\n",
            s_syncableEntities.Select(e => $"""
                SELECT '{e.EntityType}' AS "EntityType", t."Id" AS "EntityId", t."SyncStatus" AS "SyncStatus"
                FROM "{e.Table}" t
                WHERE t."UserId" = $userId
                  AND t."IsDeleted" = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM "SyncOperations" o
                      WHERE o."UserId" = $userId
                        AND o."EntityType" = '{e.EntityType}'
                        AND o."EntityId" = t."Id"
                        AND o."IsDeleted" = 0)
                """));

        // ReSharper disable once AccessToDisposedClosure - disposed with the using below.
        var results = new List<(string EntityType, Guid EntityId, SyncStatus SyncStatus)>();

        var connection = Context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = union;

            var userParameter = command.CreateParameter();
            userParameter.ParameterName = "$userId";
            userParameter.Value = userId.ToString();
            command.Parameters.Add(userParameter);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                // A row whose id is not a Guid would be a corrupted database rather
                // than a queueable one; skipping it keeps the rest of the heal
                // working instead of failing the whole sync tick.
                if (!Guid.TryParse(reader.GetString(1), out var entityId))
                    continue;

                results.Add((reader.GetString(0), entityId, (SyncStatus)reader.GetInt32(2)));
            }
        }
        finally
        {
            if (wasClosed)
                connection.Close();
        }

        return results;
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