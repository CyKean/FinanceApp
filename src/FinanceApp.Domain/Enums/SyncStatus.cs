namespace FinanceApp.Domain.Enums;

public enum SyncStatus
{
    Synced = 0,
    PendingCreate = 1,
    PendingUpdate = 2,
    PendingDelete = 3,
    Failed = 4
}