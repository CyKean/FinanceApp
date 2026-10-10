namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.DTOs;

public interface ISyncService
{
    Task<SyncStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncOperationDto>> GetRecentOperationsAsync(Guid userId, int count = 20, CancellationToken cancellationToken = default);
    Task<SyncResultDto> SyncAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SyncResultDto> ForceSyncAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsSyncingAsync(Guid userId, CancellationToken cancellationToken = default);
    void StartPeriodicSync(Guid userId);
    void StopPeriodicSync();
}