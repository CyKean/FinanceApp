namespace FinanceApp.Domain.Interfaces;

using FinanceApp.Domain.Entities;

public interface ISyncOperationRepository : IRepository<SyncOperation>
{
    Task<IReadOnlyList<SyncOperation>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncOperation>> GetFailedByUserIdAsync(Guid userId, int maxRetries, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncOperation>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
    Task<int> GetPendingCountAsync(Guid userId, CancellationToken cancellationToken = default);
}