namespace FinanceApp.Infrastructure.Supabase;

using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Infrastructure.Supabase.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Pushes local changes to Supabase via upsert (create/update) or delete.
/// When Supabase is not configured the calls are skipped so the app
/// keeps working offline on local SQLite.
/// </summary>
public class SupabaseSyncService : ISupabaseSyncService
{
    private readonly SupabaseClientProvider _clientProvider;
    private readonly ILogger<SupabaseSyncService> _logger;

    public SupabaseSyncService(SupabaseClientProvider clientProvider, ILogger<SupabaseSyncService> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var client = await _clientProvider.TryGetClientAsync(cancellationToken);
        if (client == null)
            _logger.LogWarning("Supabase not configured - sync service running offline");
        else
            _logger.LogInformation("Supabase sync service initialized");
    }

    public async Task SyncAccountAsync(Account entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
    {
        var client = await _clientProvider.TryGetClientAsync(cancellationToken);
        if (client == null)
        {
            _logger.LogDebug("Skipping account sync {AccountId} - Supabase not configured", entity.Id);
            return;
        }

        if (operationType == SyncOperationType.Delete)
        {
            await client.From<AccountRecord>().Where(r => r.Id == entity.Id).Delete();
        }
        else
        {
            await client.From<AccountRecord>().Upsert(new AccountRecord
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
                Name = entity.Name,
                Type = entity.Type.ToString(),
                BalanceAmount = entity.Balance.Amount,
                BalanceCurrency = entity.Balance.Currency,
                Description = entity.Description,
                Icon = entity.Icon,
                Color = entity.Color,
                UserId = entity.UserId,
                IsDefault = entity.IsDefault,
                SortOrder = entity.SortOrder
            });
        }

        _logger.LogDebug("Synced account {AccountId} ({OperationType})", entity.Id, operationType);
    }

    public async Task SyncCategoryAsync(Category entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
    {
        var client = await _clientProvider.TryGetClientAsync(cancellationToken);
        if (client == null)
        {
            _logger.LogDebug("Skipping category sync {CategoryId} - Supabase not configured", entity.Id);
            return;
        }

        if (operationType == SyncOperationType.Delete)
        {
            await client.From<CategoryRecord>().Where(r => r.Id == entity.Id).Delete();
        }
        else
        {
            await client.From<CategoryRecord>().Upsert(new CategoryRecord
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
                Name = entity.Name,
                Type = entity.Type.ToString(),
                Icon = entity.Icon,
                Color = entity.Color,
                ParentCategoryId = entity.ParentCategoryId,
                UserId = entity.UserId,
                IsSystem = entity.IsSystem,
                SortOrder = entity.SortOrder,
                IsActive = entity.IsActive
            });
        }

        _logger.LogDebug("Synced category {CategoryId} ({OperationType})", entity.Id, operationType);
    }

    public async Task SyncTransactionAsync(Transaction entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
    {
        var client = await _clientProvider.TryGetClientAsync(cancellationToken);
        if (client == null)
        {
            _logger.LogDebug("Skipping transaction sync {TransactionId} - Supabase not configured", entity.Id);
            return;
        }

        if (operationType == SyncOperationType.Delete)
        {
            await client.From<TransactionRecord>().Where(r => r.Id == entity.Id).Delete();
        }
        else
        {
            await client.From<TransactionRecord>().Upsert(new TransactionRecord
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
                Type = entity.Type.ToString(),
                Amount = entity.Amount.Amount,
                Currency = entity.Amount.Currency,
                Date = entity.Date,
                Notes = entity.Notes,
                AccountId = entity.AccountId,
                CategoryId = entity.CategoryId,
                UserId = entity.UserId,
                RecurringTransactionId = entity.RecurringTransactionId
            });
        }

        _logger.LogDebug("Synced transaction {TransactionId} ({OperationType})", entity.Id, operationType);
    }

    public async Task SyncBudgetAsync(Budget entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
    {
        var client = await _clientProvider.TryGetClientAsync(cancellationToken);
        if (client == null)
        {
            _logger.LogDebug("Skipping budget sync {BudgetId} - Supabase not configured", entity.Id);
            return;
        }

        if (operationType == SyncOperationType.Delete)
        {
            await client.From<BudgetRecord>().Where(r => r.Id == entity.Id).Delete();
        }
        else
        {
            await client.From<BudgetRecord>().Upsert(new BudgetRecord
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
                Name = entity.Name,
                Amount = entity.Amount.Amount,
                Currency = entity.Amount.Currency,
                SpentAmount = entity.SpentAmount.Amount,
                StartDate = entity.StartDate,
                EndDate = entity.EndDate,
                CategoryId = entity.CategoryId,
                UserId = entity.UserId
            });
        }

        _logger.LogDebug("Synced budget {BudgetId} ({OperationType})", entity.Id, operationType);
    }

    public async Task SyncRecurringTransactionAsync(RecurringTransaction entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
    {
        var client = await _clientProvider.TryGetClientAsync(cancellationToken);
        if (client == null)
        {
            _logger.LogDebug("Skipping recurring transaction sync {RecurringTransactionId} - Supabase not configured", entity.Id);
            return;
        }

        if (operationType == SyncOperationType.Delete)
        {
            await client.From<RecurringTransactionRecord>().Where(r => r.Id == entity.Id).Delete();
        }
        else
        {
            await client.From<RecurringTransactionRecord>().Upsert(new RecurringTransactionRecord
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
                Name = entity.Name,
                Type = entity.Type.ToString(),
                Amount = entity.Amount.Amount,
                Currency = entity.Amount.Currency,
                Frequency = entity.Frequency.ToString(),
                StartDate = entity.StartDate,
                EndDate = entity.EndDate,
                AccountId = entity.AccountId,
                CategoryId = entity.CategoryId,
                UserId = entity.UserId,
                Notes = entity.Notes,
                LastGeneratedAt = entity.LastGeneratedAt,
                NextDueDate = entity.NextDueDate,
                IsActive = entity.IsActive
            });
        }

        _logger.LogDebug("Synced recurring transaction {RecurringTransactionId} ({OperationType})", entity.Id, operationType);
    }

    public async Task SyncFinancialGoalAsync(FinancialGoal entity, SyncOperationType operationType, CancellationToken cancellationToken = default)
    {
        var client = await _clientProvider.TryGetClientAsync(cancellationToken);
        if (client == null)
        {
            _logger.LogDebug("Skipping financial goal sync {FinancialGoalId} - Supabase not configured", entity.Id);
            return;
        }

        if (operationType == SyncOperationType.Delete)
        {
            await client.From<FinancialGoalRecord>().Where(r => r.Id == entity.Id).Delete();
        }
        else
        {
            await client.From<FinancialGoalRecord>().Upsert(new FinancialGoalRecord
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
                Name = entity.Name,
                TargetAmount = entity.TargetAmount.Amount,
                TargetCurrency = entity.TargetAmount.Currency,
                CurrentAmount = entity.CurrentAmount.Amount,
                TargetDate = entity.TargetDate,
                StartDate = entity.StartDate,
                Status = entity.Status.ToString(),
                Description = entity.Description,
                Icon = entity.Icon,
                Color = entity.Color,
                UserId = entity.UserId,
                LinkedAccountId = entity.LinkedAccountId.HasValue ? entity.LinkedAccountId.Value.Value : null
            });
        }

        _logger.LogDebug("Synced financial goal {FinancialGoalId} ({OperationType})", entity.Id, operationType);
    }

    public Task SyncSyncOperationAsync(SyncOperation entity, CancellationToken cancellationToken = default)
    {
        // Sync operations are local bookkeeping for the offline queue -
        // they are not pushed to the server.
        _logger.LogDebug("Sync operation {SyncOperationId} recorded locally", entity.Id);
        return Task.CompletedTask;
    }
}
