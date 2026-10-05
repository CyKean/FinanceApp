namespace FinanceApp.Application.DTOs;

using FinanceApp.Domain.Enums;

public record SyncOperationDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    SyncOperationType OperationType,
    SyncStatus Status,
    int RetryCount,
    DateTime? LastAttemptAt,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SyncStatusDto(
    bool IsSyncing,
    DateTime? LastSyncAt,
    int PendingCount,
    int FailedCount,
    string? LastError
);

/// <param name="Success">False when anything failed or was deferred. Deferred work
/// is not a failure, but reporting it as a success would promise the user their
/// changes reached the cloud when none were sent.</param>
/// <param name="DeferredCount">Operations that were not attempted because the cloud
/// could not take them yet - no session, no reachable project, or a local id that
/// does not match the signed-in one. They are still pending and untouched.</param>
public record SyncResultDto(
    bool Success,
    int SyncedCount,
    int FailedCount,
    string? ErrorMessage,
    int PulledCount = 0,
    int DeferredCount = 0
);