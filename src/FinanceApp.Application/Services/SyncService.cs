namespace FinanceApp.Application.Services;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

public class SyncService : BaseService, ISyncService
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
    }

    public async Task<SyncStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var pendingCount = await _syncRepository.GetPendingCountAsync(userId, cancellationToken);
        var failedOperations = await _syncRepository.GetFailedByUserIdAsync(userId, 5, cancellationToken);
        var lastSync = await GetLastSuccessfulSyncAsync(userId, cancellationToken);

        return new SyncStatusDto(
            false,
            lastSync,
            pendingCount,
            failedOperations.Count,
            failedOperations.FirstOrDefault()?.ErrorMessage);
    }

    public async Task<SyncResultDto> SyncAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_connectivityService.CurrentAccess != NetworkAccess.Internet)
        {
            return new SyncResultDto(false, 0, 0, "No internet connection");
        }

        if (!await _syncLock.WaitAsync(0, cancellationToken))
        {
            return new SyncResultDto(false, 0, 0, "Sync already in progress");
        }

        try
        {
            _logger.LogInformation("Starting sync for user {UserId}", userId);

            var pendingOperations = await _syncRepository.GetPendingByUserIdAsync(userId, cancellationToken);
            if (!pendingOperations.Any())
            {
                _logger.LogInformation("No pending operations for user {UserId}", userId);
                return new SyncResultDto(true, 0, 0, null);
            }

            var syncedCount = 0;
            var failedCount = 0;
            string? lastError = null;

            foreach (var operation in pendingOperations)
            {
                try
                {
                    await ProcessSyncOperationAsync(operation, userId, cancellationToken);
                    operation.MarkAsSynced();
                    await _syncRepository.UpdateAsync(operation, cancellationToken);
                    syncedCount++;
                }
                catch (Exception ex)
                {
                    operation.IncrementRetry(ex.Message);
                    await _syncRepository.UpdateAsync(operation, cancellationToken);
                    failedCount++;
                    lastError = ex.Message;
                    _logger.LogError(ex, "Failed to sync operation {OperationId} for user {UserId}", operation.Id, userId);
                }
            }

            await UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Sync completed for user {UserId}: {Synced} synced, {Failed} failed", userId, syncedCount, failedCount);

            return new SyncResultDto(failedCount == 0, syncedCount, failedCount, lastError);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task ForceSyncAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await SyncAsync(userId, cancellationToken);
    }

    public async Task<bool> IsSyncingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _syncLock.CurrentCount == 0;
    }

    private async Task ProcessSyncOperationAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
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
    }

    private async Task SyncAccountAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        var account = await _accountRepository.GetByIdAsync(operation.EntityId, cancellationToken);
        if (account == null || account.UserId != userId)
            throw new NotFoundException("Account", operation.EntityId);

        await _supabaseSyncService.SyncAccountAsync(account, operation.OperationType, cancellationToken);
    }

    private async Task SyncCategoryAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(operation.EntityId, cancellationToken);
        if (category == null || category.UserId != userId)
            throw new NotFoundException("Category", operation.EntityId);

        await _supabaseSyncService.SyncCategoryAsync(category, operation.OperationType, cancellationToken);
    }

    private async Task SyncTransactionAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        var transaction = await _transactionRepository.GetByIdAsync(operation.EntityId, cancellationToken);
        if (transaction == null || transaction.UserId != userId)
            throw new NotFoundException("Transaction", operation.EntityId);

        await _supabaseSyncService.SyncTransactionAsync(transaction, operation.OperationType, cancellationToken);
    }

    private async Task SyncBudgetAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        var budget = await _budgetRepository.GetByIdAsync(operation.EntityId, cancellationToken);
        if (budget == null || budget.UserId != userId)
            throw new NotFoundException("Budget", operation.EntityId);

        await _supabaseSyncService.SyncBudgetAsync(budget, operation.OperationType, cancellationToken);
    }

    private async Task SyncRecurringTransactionAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        var recurring = await _recurringRepository.GetByIdAsync(operation.EntityId, cancellationToken);
        if (recurring == null || recurring.UserId != userId)
            throw new NotFoundException("RecurringTransaction", operation.EntityId);

        await _supabaseSyncService.SyncRecurringTransactionAsync(recurring, operation.OperationType, cancellationToken);
    }

    private async Task SyncFinancialGoalAsync(SyncOperation operation, Guid userId, CancellationToken cancellationToken)
    {
        var goal = await _goalRepository.GetByIdAsync(operation.EntityId, cancellationToken);
        if (goal == null || goal.UserId != userId)
            throw new NotFoundException("FinancialGoal", operation.EntityId);

        await _supabaseSyncService.SyncFinancialGoalAsync(goal, operation.OperationType, cancellationToken);
    }

    private async Task<DateTime?> GetLastSuccessfulSyncAsync(Guid userId, CancellationToken cancellationToken)
    {
        var syncedOps = await _syncRepository.GetPendingByUserIdAsync(userId, cancellationToken);
        return syncedOps
            .Where(o => o.Status == SyncStatus.Synced)
            .OrderByDescending(o => o.LastAttemptAt)
            .Select(o => o.LastAttemptAt)
            .FirstOrDefault();
    }
}