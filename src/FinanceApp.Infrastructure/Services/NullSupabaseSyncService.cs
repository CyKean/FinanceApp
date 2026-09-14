namespace FinanceApp.Infrastructure.Services;

using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.Extensions.Logging;

public class NullSupabaseSyncService : ISupabaseSyncService
{
    private readonly ILogger<NullSupabaseSyncService> _logger;

    public NullSupabaseSyncService(ILogger<NullSupabaseSyncService> logger)
    {
        _logger = logger;
    }

    public Task SyncAccountAsync(Account entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Supabase sync not implemented - Account {Operation}", operationType);
        return Task.CompletedTask;
    }

    public Task SyncCategoryAsync(Category entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Supabase sync not implemented - Category {Operation}", operationType);
        return Task.CompletedTask;
    }

    public Task SyncTransactionAsync(Transaction entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Supabase sync not implemented - Transaction {Operation}", operationType);
        return Task.CompletedTask;
    }

    public Task SyncBudgetAsync(Budget entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Supabase sync not implemented - Budget {Operation}", operationType);
        return Task.CompletedTask;
    }

    public Task SyncRecurringTransactionAsync(RecurringTransaction entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Supabase sync not implemented - RecurringTransaction {Operation}", operationType);
        return Task.CompletedTask;
    }

    public Task SyncFinancialGoalAsync(FinancialGoal entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Supabase sync not implemented - FinancialGoal {Operation}", operationType);
        return Task.CompletedTask;
    }
}