namespace FinanceApp.Application.Interfaces;

using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;

public interface ISupabaseSyncService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SyncAccountAsync(Account entity, SyncOperationType operationType, CancellationToken cancellationToken = default);
    Task SyncCategoryAsync(Category entity, SyncOperationType operationType, CancellationToken cancellationToken = default);
    Task SyncTransactionAsync(Transaction entity, SyncOperationType operationType, CancellationToken cancellationToken = default);
    Task SyncBudgetAsync(Budget entity, SyncOperationType operationType, CancellationToken cancellationToken = default);
    Task SyncRecurringTransactionAsync(RecurringTransaction entity, SyncOperationType operationType, CancellationToken cancellationToken = default);
    Task SyncFinancialGoalAsync(FinancialGoal entity, SyncOperationType operationType, CancellationToken cancellationToken = default);
    Task SyncSyncOperationAsync(SyncOperation entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads server rows and merges them into SQLite (last-write-wins).
    /// Returns the number of rows applied locally.
    /// </summary>
    Task<int> PullAsync(Guid userId, CancellationToken cancellationToken = default);
}