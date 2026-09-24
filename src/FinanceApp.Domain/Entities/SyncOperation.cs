namespace FinanceApp.Domain.Entities;

using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;

public class SyncOperation : Entity
{
    public string EntityType { get; private set; }
    public Guid EntityId { get; private set; }
    public SyncOperationType OperationType { get; private set; }
    public SyncStatus Status { get; private set; }
    public string? Payload { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? LastAttemptAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid UserId { get; private set; }

    private SyncOperation() : base() { }

    public SyncOperation(
        string entityType,
        Guid entityId,
        SyncOperationType operationType,
        Guid userId,
        string? payload = null) : base()
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("EntityType is required", nameof(entityType));

        if (entityId == Guid.Empty)
            throw new ArgumentException("EntityId is required", nameof(entityId));

        EntityType = entityType;
        EntityId = entityId;
        OperationType = operationType;
        UserId = userId;
        Status = SyncStatus.PendingCreate;
        Payload = payload;
        RetryCount = 0;
    }

    public void IncrementRetry(string? errorMessage = null)
    {
        RetryCount++;
        LastAttemptAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
        Status = SyncStatus.Failed;
        UpdateTimestamp();
    }

    public void MarkAsSynced()
    {
        Status = SyncStatus.Synced;
        LastAttemptAt = DateTime.UtcNow;
        ErrorMessage = null;
        UpdateTimestamp();
    }

    public void MarkAsPending()
    {
        Status = OperationType switch
        {
            SyncOperationType.Create => SyncStatus.PendingCreate,
            SyncOperationType.Update => SyncStatus.PendingUpdate,
            SyncOperationType.Delete => SyncStatus.PendingDelete,
            _ => SyncStatus.PendingCreate
        };
        UpdateTimestamp();
    }

    public void SetPayload(string payload)
    {
        Payload = payload;
        UpdateTimestamp();
    }

    public bool HasExceededMaxRetries(int maxRetries = 5)
    {
        return RetryCount >= maxRetries;
    }

    public void ResetForRetry()
    {
        RetryCount = 0;
        Status = SyncStatus.PendingCreate;
        ErrorMessage = null;
        LastAttemptAt = null;
        UpdateTimestamp();
    }
}