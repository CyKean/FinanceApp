namespace FinanceApp.Application.Services;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

public class SyncService : BaseService, ISyncService, IDisposable
{
    private readonly ISyncOperationRepository _syncRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IRecurringTransactionRepository _recurringRepository;
    private readonly IFinancialGoalRepository _goalRepository;
    private readonly ISupabaseSyncService _supabaseSyncService;
    private readonly IConnectivityService _connectivityService;
    private readonly ILogger<SyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly Timer? _periodicSyncTimer;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5);
    private readonly int _maxRetries = 5;
    private readonly TimeSpan _baseRetryDelay = TimeSpan.FromSeconds(2);
    private Guid? _currentUserId;
    private CancellationTokenSource? _syncCts;

    public SyncService(
        IUnitOfWork unitOfWork,
        ISyncOperationRepository syncRepository,
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        IBudgetRepository budgetRepository,
        IRecurringTransactionRepository recurringRepository,
        IFinancialGoalRepository goalRepository,
        ISupabaseSyncService supabaseSyncService,
        IConnectivityService connectivityService,
        ILogger<SyncService> logger) : base(unitOfWork, logger)
    {
        _syncRepository = syncRepository;
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
        _budgetRepository = budgetRepository;
        _recurringRepository = recurringRepository;
        _goalRepository = goalRepository;
        _supabaseSyncService = supabaseSyncService;
        _connectivityService = connectivityService;
        _logger = logger;

        _connectivityService.ConnectivityChanged += OnConnectivityChanged;
        _periodicSyncTimer = new Timer(PeriodicSyncCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task<SyncStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var pendingCount = await _syncRepository.GetPendingCountAsync(userId, cancellationToken);
        var failedOperations = await _syncRepository.GetFailedByUserIdAsync(userId, _maxRetries, cancellationToken);
        var lastSync = await GetLastSuccessfulSyncAsync(userId, cancellationToken);

        return new SyncStatusDto(
            _syncLock.CurrentCount == 0,
            lastSync,
            pendingCount,
            failedOperations.Count,
            failedOperations.FirstOrDefault()?.ErrorMessage);
    }

    public async Task<IReadOnlyList<SyncOperationDto>> GetRecentOperationsAsync(Guid userId, int count = 20, CancellationToken cancellationToken = default)
    {
        var operations = await _syncRepository.GetRecentByUserIdAsync(userId, count, cancellationToken);
        return operations
            .Select(o => new SyncOperationDto(
                o.Id,
                o.EntityType,
                o.EntityId,
                o.OperationType,
                o.Status,
                o.RetryCount,
                o.LastAttemptAt,
                o.ErrorMessage,
                o.CreatedAt,
                o.UpdatedAt))
            .ToList();
    }

    public async Task<SyncResultDto> SyncAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _currentUserId = userId;
        _syncCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (await _connectivityService.CheckConnectivityAsync(cancellationToken) != NetworkAccess.Internet)
        {
            return new SyncResultDto(false, 0, 0, "No internet connection");
        }

        if (!await _syncLock.WaitAsync(0, _syncCts.Token))
        {
            return new SyncResultDto(false, 0, 0, "Sync already in progress");
        }

        try
        {
            _logger.LogInformation("Starting sync for user {UserId}", userId);

            // Heal rows that never got outbox entries (pre-outbox data, seeds):
            // anything in SQLite without an operation gets a Create op so the
            // whole local database eventually lands in Supabase.
            await EnqueueMissingOperationsAsync(userId, _syncCts.Token);

            var pendingOperations = await _syncRepository.GetPendingByUserIdAsync(userId, _syncCts.Token);
            if (!pendingOperations.Any())
            {
                _logger.LogInformation("No pending operations for user {UserId}", userId);
                return new SyncResultDto(true, 0, 0, null);
            }

            // Collapse duplicate ops for the same row (edit x3 offline = 1 push).
            // A Delete anywhere in the group wins; otherwise the latest op covers all.
            pendingOperations = await CoalesceOperationsAsync(pendingOperations, _syncCts.Token);
            if (!pendingOperations.Any())
            {
                await UnitOfWork.SaveChangesAsync(_syncCts.Token);
                return new SyncResultDto(true, 0, 0, null);
            }

            var syncedCount = 0;
            var pushedCount = 0;
            var pulledCount = 0;
            var failedCount = 0;
            string? lastError = null;

            foreach (var operation in pendingOperations)
            {
                if (_syncCts.Token.IsCancellationRequested)
                    break;

                try
                {
                    await ProcessSyncOperationWithRetryAsync(operation, userId, _syncCts.Token);
                    operation.MarkAsSynced();
                    await _syncRepository.UpdateAsync(operation, _syncCts.Token);
                    syncedCount++;
                    pushedCount++;
                }
                catch (Exception ex)
                {
                    operation.IncrementRetry(SyncErrorSanitizer.Sanitize(ex.Message));
                    await _syncRepository.UpdateAsync(operation, _syncCts.Token);
                    failedCount++;
                    lastError = SyncErrorSanitizer.Sanitize(ex.Message);
                    _logger.LogError(ex, "Failed to sync operation {OperationId} for user {UserId}", operation.Id, userId);
                }
            }

            await UnitOfWork.SaveChangesAsync(_syncCts.Token);

            // Pull after pushing: server rows newer than local (and not locally
            // pending) overwrite SQLite, so the UI - which reads SQLite only -
            // always shows the merged result.
            try
            {
                var pulled = await _supabaseSyncService.PullAsync(userId, _syncCts.Token);
                pulledCount = pulled;
                syncedCount += pulled;
            }
            catch (Exception ex)
            {
                failedCount++;
                lastError = SyncErrorSanitizer.Sanitize(ex.Message);
                _logger.LogError(ex, "Pull failed for user {UserId}", userId);
            }

            _logger.LogInformation("Sync completed for user {UserId}: {Synced} synced ({Pushed} pushed, {Pulled} pulled), {Failed} failed", userId, syncedCount, pushedCount, pulledCount, failedCount);

            return new SyncResultDto(failedCount == 0, pushedCount, failedCount, lastError, pulledCount);
        }
        catch (OperationCanceledException)
        {
            return new SyncResultDto(false, 0, 0, "Sync cancelled");
        }
        finally
        {
            _syncLock.Release();
            _syncCts?.Dispose();
            _syncCts = null;
            _currentUserId = null;
        }
    }

    public async Task ForceSyncAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Reset retry counts for failed operations to allow retry
        var failedOps = await _syncRepository.GetFailedByUserIdAsync(userId, _maxRetries, cancellationToken);
        foreach (var op in failedOps)
        {
            op.ResetForRetry();
            await _syncRepository.UpdateAsync(op, cancellationToken);
        }
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        
        await SyncAsync(userId, cancellationToken);
    }

    public async Task<bool> IsSyncingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _syncLock.CurrentCount == 0;
    }

    public void StartPeriodicSync(Guid userId)
    {
        _currentUserId = userId;
        _periodicSyncTimer?.Change(_syncInterval, _syncInterval);
        _logger.LogInformation("Started periodic sync for user {UserId} every {Interval}", userId, _syncInterval);
    }

    public void StopPeriodicSync()
    {
        _periodicSyncTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _currentUserId = null;
        _logger.LogInformation("Stopped periodic sync");
    }

    private async void PeriodicSyncCallback(object? state)
    {
        if (!_currentUserId.HasValue) return;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await SyncAsync(_currentUserId.Value, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Periodic sync failed for user {UserId}", _currentUserId);
        }
    }

    private void OnConnectivityChanged(ConnectivityChangedEventArgs e)
    {
        _logger.LogInformation("Connectivity changed: {Previous} -> {Current}", e.PreviousAccess, e.CurrentAccess);

        if (e.CurrentAccess == NetworkAccess.Internet && _currentUserId.HasValue)
        {
            // Trigger sync when connectivity is restored
            _ = Task.Run(async () =>
            {
                try
                {
                    await SyncAsync(_currentUserId.Value, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-sync after connectivity restored failed");
                }
            });
        }
    }

    private async Task EnqueueMissingOperationsAsync(Guid userId, CancellationToken cancellationToken)
    {
// Fetched once and consulted in memory below. It used to be a query per
        // entity, so every sync run cost one round-trip per row the user owns, on
        // a loop that runs every minute.
        var tracked = await _syncRepository.GetTrackedEntitiesAsync(userId, cancellationToken);
        var changed = false;

        changed |= await EnqueueMissingAsync("Account", await _accountRepository.GetByUserIdAsync(userId, cancellationToken), userId, tracked, cancellationToken);
        changed |= await EnqueueMissingAsync("Category", await _categoryRepository.GetByUserIdAsync(userId, cancellationToken), userId, tracked, cancellationToken);
        changed |= await EnqueueMissingAsync("Transaction", await _transactionRepository.GetByUserIdAsync(userId, cancellationToken), userId, tracked, cancellationToken);
        changed |= await EnqueueMissingAsync("Budget", await _budgetRepository.GetByUserIdAsync(userId, cancellationToken), userId, tracked, cancellationToken);
        changed |= await EnqueueMissingAsync("RecurringTransaction", await _recurringRepository.GetByUserIdAsync(userId, cancellationToken), userId, tracked, cancellationToken);
        changed |= await EnqueueMissingAsync("FinancialGoal", await _goalRepository.GetByUserIdAsync(userId, cancellationToken), userId, tracked, cancellationToken);

        // Only when something was actually queued. Saving unconditionally took
        // SQLite's write lock on every sync tick even when the outbox was whole.
        if (changed)
        {
            await UnitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<bool> EnqueueMissingAsync<T>(
        string entityType,
        IReadOnlyList<T> entities,
        Guid userId,
        IReadOnlySet<(string EntityType, Guid EntityId)> tracked,
        CancellationToken cancellationToken)
        where T : Entity
    {
        var changed = false;

        foreach (var entity in entities)
        {
            if (tracked.Contains((entityType, entity.Id)))
                continue;

            var operationType = entity.SyncStatus == SyncStatus.PendingDelete
                ? SyncOperationType.Delete
                : SyncOperationType.Create;

            await _syncRepository.AddAsync(
                new SyncOperation(entityType, entity.Id, operationType, userId),
                cancellationToken);

            changed = true;
        }

        return changed;
    }

    private async Task<IReadOnlyList<SyncOperation>> CoalesceOperationsAsync(
        IReadOnlyList<SyncOperation> pending,
        CancellationToken cancellationToken)
    {
        var survivors = new List<SyncOperation>();

        foreach (var group in pending.GroupBy(o => (o.EntityType, o.EntityId)))
        {
            var ordered = group.OrderBy(o => o.CreatedAt).ToList();
            if (ordered.Count == 1)
            {
                survivors.Add(ordered[0]);
                continue;
            }

            var survivor = ordered.LastOrDefault(o => o.OperationType == SyncOperationType.Delete)
                ?? ordered[^1];

            foreach (var redundant in ordered.Where(o => o != survivor))
            {
                redundant.MarkAsSynced();
                await _syncRepository.UpdateAsync(redundant, cancellationToken);
            }

            survivors.Add(survivor);
            _logger.LogInformation(
                "Coalesced {Count} pending ops for {EntityType} {EntityId} into {Operation}",
                ordered.Count, group.Key.EntityType, group.Key.EntityId, survivor.OperationType);
        }

        await UnitOfWork.SaveChangesAsync(cancellationToken);
        return survivors.OrderBy(o => o.CreatedAt).ToList();
    }

    private async Task ProcessSyncOperationWithRetryAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        var retryDelay = _baseRetryDelay;
        
        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                await ProcessSyncOperationAsync(operation, userId, cancellationToken);
                return; // Success
            }
            catch (Exception ex) when (attempt < _maxRetries && IsTransientError(ex))
            {
                operation.IncrementRetry($"Attempt {attempt + 1}: {SyncErrorSanitizer.Sanitize(ex.Message)}");
                await _syncRepository.UpdateAsync(operation, cancellationToken);
                await UnitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(ex, "Sync attempt {Attempt} failed for operation {OperationId}, retrying in {Delay}", 
                    attempt + 1, operation.Id, retryDelay);

                await Task.Delay(retryDelay, cancellationToken);
                retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, 60)); // Exponential backoff, max 60s
            }
        }

        // All retries exhausted
        throw new InvalidOperationException($"Sync operation {operation.Id} failed after {_maxRetries} retries");
    }

    private bool IsTransientError(Exception ex)
    {
        return ex is HttpRequestException 
            || ex is TaskCanceledException 
            || ex is TimeoutException
            || (ex is InvalidOperationException && ex.Message.Contains("transient", StringComparison.OrdinalIgnoreCase));
    }

    private async Task ProcessSyncOperationAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        // Idempotency: Check if this operation was already processed
        if (await IsOperationProcessedAsync(operation, cancellationToken))
        {
            _logger.LogInformation("Operation {OperationId} already processed, skipping", operation.Id);
            return;
        }

        switch (operation.EntityType)
        {
            case "Account":
                await SyncAccountAsync(operation, userId, cancellationToken);
                break;
            case "Category":
                await SyncCategoryAsync(operation, userId, cancellationToken);
                break;
            case "Transaction":
                await SyncTransactionAsync(operation, userId, cancellationToken);
                break;
            case "Budget":
                await SyncBudgetAsync(operation, userId, cancellationToken);
                break;
            case "RecurringTransaction":
                await SyncRecurringTransactionAsync(operation, userId, cancellationToken);
                break;
            case "FinancialGoal":
                await SyncFinancialGoalAsync(operation, userId, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unknown entity type: {operation.EntityType}");
        }

        // Mark operation as processed for idempotency
        await MarkOperationProcessedAsync(operation, cancellationToken);
    }

    private async Task<bool> IsOperationProcessedAsync(SyncOperation operation, CancellationToken cancellationToken)
    {
        // Check if entity is already in synced state with same or higher version
        var entity = await GetEntityAsync(operation.EntityType, operation.EntityId, cancellationToken);
        if (entity == null) return false;

        // If entity is synced and version matches or exceeds, operation was processed
        return entity.SyncStatus == SyncStatus.Synced && entity.Version >= operation.Version;
    }

    private async Task MarkOperationProcessedAsync(SyncOperation operation, CancellationToken cancellationToken)
    {
        // The operation itself is marked as Synced by the caller
        // We could also maintain a separate idempotency key store if needed
    }

    private async Task<Entity?> GetEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        return entityType switch
        {
            "Account" => await _accountRepository.GetByIdAsync(entityId, cancellationToken),
            "Category" => await _categoryRepository.GetByIdAsync(entityId, cancellationToken),
            "Transaction" => await _transactionRepository.GetByIdAsync(entityId, cancellationToken),
            "Budget" => await _budgetRepository.GetByIdAsync(entityId, cancellationToken),
            "RecurringTransaction" => await _recurringRepository.GetByIdAsync(entityId, cancellationToken),
            "FinancialGoal" => await _goalRepository.GetByIdAsync(entityId, cancellationToken),
            _ => null
        };
    }

    private async Task SyncAccountAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        var account = await _accountRepository.GetByIdIncludingDeletedAsync(operation.EntityId, cancellationToken);
        if (account == null)
        {
            _logger.LogWarning("Account {EntityId} not found locally, treating operation {OperationId} as synced", operation.EntityId, operation.Id);
            return;
        }

        if (account.UserId != userId)
            throw new NotFoundException("Account", operation.EntityId);

        var operationType = account.IsDeleted ? SyncOperationType.Delete : operation.OperationType;

        if (operationType == SyncOperationType.Update)
        {
            await ResolveConflictAsync(account, operation, cancellationToken);
        }

        await _supabaseSyncService.SyncAccountAsync(account, operationType, cancellationToken);
        account.MarkAsSynced();
        await _accountRepository.UpdateAsync(account, cancellationToken);
    }

    private async Task SyncCategoryAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdIncludingDeletedAsync(operation.EntityId, cancellationToken);
        if (category == null)
        {
            _logger.LogWarning("Category {EntityId} not found locally, treating operation {OperationId} as synced", operation.EntityId, operation.Id);
            return;
        }

        if (category.UserId != userId)
            throw new NotFoundException("Category", operation.EntityId);

        var operationType = category.IsDeleted ? SyncOperationType.Delete : operation.OperationType;

        if (operationType == SyncOperationType.Update)
        {
            await ResolveConflictAsync(category, operation, cancellationToken);
        }

        await _supabaseSyncService.SyncCategoryAsync(category, operationType, cancellationToken);
        category.MarkAsSynced();
        await _categoryRepository.UpdateAsync(category, cancellationToken);
    }

    private async Task SyncTransactionAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        var transaction = await _transactionRepository.GetByIdIncludingDeletedAsync(operation.EntityId, cancellationToken);
        if (transaction == null)
        {
            _logger.LogWarning("Transaction {EntityId} not found locally, treating operation {OperationId} as synced", operation.EntityId, operation.Id);
            return;
        }

        if (transaction.UserId != userId)
            throw new NotFoundException("Transaction", operation.EntityId);

        var operationType = transaction.IsDeleted ? SyncOperationType.Delete : operation.OperationType;

        if (operationType == SyncOperationType.Update)
        {
            await ResolveConflictAsync(transaction, operation, cancellationToken);
        }

        await _supabaseSyncService.SyncTransactionAsync(transaction, operationType, cancellationToken);
        transaction.MarkAsSynced();
        await _transactionRepository.UpdateAsync(transaction, cancellationToken);
    }

    private async Task SyncBudgetAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        var budget = await _budgetRepository.GetByIdIncludingDeletedAsync(operation.EntityId, cancellationToken);
        if (budget == null)
        {
            _logger.LogWarning("Budget {EntityId} not found locally, treating operation {OperationId} as synced", operation.EntityId, operation.Id);
            return;
        }

        if (budget.UserId != userId)
            throw new NotFoundException("Budget", operation.EntityId);

        var operationType = budget.IsDeleted ? SyncOperationType.Delete : operation.OperationType;

        if (operationType == SyncOperationType.Update)
        {
            await ResolveConflictAsync(budget, operation, cancellationToken);
        }

        await _supabaseSyncService.SyncBudgetAsync(budget, operationType, cancellationToken);
        budget.MarkAsSynced();
        await _budgetRepository.UpdateAsync(budget, cancellationToken);
    }

    private async Task SyncRecurringTransactionAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        var recurring = await _recurringRepository.GetByIdIncludingDeletedAsync(operation.EntityId, cancellationToken);
        if (recurring == null)
        {
            _logger.LogWarning("RecurringTransaction {EntityId} not found locally, treating operation {OperationId} as synced", operation.EntityId, operation.Id);
            return;
        }

        if (recurring.UserId != userId)
            throw new NotFoundException("RecurringTransaction", operation.EntityId);

        var operationType = recurring.IsDeleted ? SyncOperationType.Delete : operation.OperationType;

        if (operationType == SyncOperationType.Update)
        {
            await ResolveConflictAsync(recurring, operation, cancellationToken);
        }

        await _supabaseSyncService.SyncRecurringTransactionAsync(recurring, operationType, cancellationToken);
        recurring.MarkAsSynced();
        await _recurringRepository.UpdateAsync(recurring, cancellationToken);
    }

    private async Task SyncFinancialGoalAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        var goal = await _goalRepository.GetByIdIncludingDeletedAsync(operation.EntityId, cancellationToken);
        if (goal == null)
        {
            _logger.LogWarning("FinancialGoal {EntityId} not found locally, treating operation {OperationId} as synced", operation.EntityId, operation.Id);
            return;
        }

        if (goal.UserId != userId)
            throw new NotFoundException("FinancialGoal", operation.EntityId);

        var operationType = goal.IsDeleted ? SyncOperationType.Delete : operation.OperationType;

        if (operationType == SyncOperationType.Update)
        {
            await ResolveConflictAsync(goal, operation, cancellationToken);
        }

        await _supabaseSyncService.SyncFinancialGoalAsync(goal, operationType, cancellationToken);
        goal.MarkAsSynced();
        await _goalRepository.UpdateAsync(goal, cancellationToken);
    }

    private async Task ResolveConflictAsync(Entity localEntity, SyncOperation operation, CancellationToken cancellationToken)
    {
        // Last-Write-Wins with version vector conflict resolution
        // If local version is newer, push local changes
        // If server version is newer, we'd need to fetch server state (not implemented in stub)
        // For now, always push local (last-write-wins based on UpdatedAt)
        
        if (localEntity.Version < operation.Version)
        {
            _logger.LogWarning("Conflict detected for {EntityType} {EntityId}: local version {LocalVer} < operation version {OpVer}. Pushing local changes anyway (LWW).",
                operation.EntityType, operation.EntityId, localEntity.Version, operation.Version);
        }

        // In a real implementation, you would:
        // 1. Fetch server entity
        // 2. Compare versions/timestamps
        // 3. Merge or prompt user
        // 4. Apply resolution
    }

    private async Task<DateTime?> GetLastSuccessfulSyncAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _syncRepository.GetLastSyncedAtAsync(userId, cancellationToken);
    }

    public void Dispose()
    {
        _periodicSyncTimer?.Dispose();
        _syncLock.Dispose();
        _syncCts?.Dispose();
        _connectivityService.ConnectivityChanged -= OnConnectivityChanged;
    }
}