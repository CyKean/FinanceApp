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

public record SyncResultDto(
    bool Success,
    int SyncedCount,
    int FailedCount,
    string? ErrorMessage,
    int PulledCount = 0
);