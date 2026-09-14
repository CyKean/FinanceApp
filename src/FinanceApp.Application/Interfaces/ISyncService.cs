namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.DTOs;

public interface ISyncService
{
    Task<SyncStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SyncResultDto> SyncAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ForceSyncAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsSyncingAsync(Guid userId, CancellationToken cancellationToken = default);
}