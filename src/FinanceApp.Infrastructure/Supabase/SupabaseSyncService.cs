namespace FinanceApp.Infrastructure.Supabase;

using System.Text.RegularExpressions;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Infrastructure.Supabase.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Pushes local changes to Supabase via upsert (create/update) or delete.
/// When Supabase is not configured the calls are skipped so the app
/// keeps working offline on local SQLite.
/// </summary>
public class SupabaseSyncService : ISupabaseSyncService
{
    private static readonly Regex MissingColumnRegex = new(
        @"Could not find the '(?<column>[^']+)' column of '(?<table>[^']+)'",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] BudgetOptionalColumns = { "icon", "color", "linked_account_id" };

    private readonly SupabaseClientProvider _clientProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SupabaseSyncService> _logger;
    private volatile bool _budgetPushOmitsOptionalColumns;

    public SupabaseSyncService(
        SupabaseClientProvider clientProvider,
        IUnitOfWork unitOfWork,
        ILogger<SupabaseSyncService> logger)
    {
        _clientProvider = clientProvider;
        _unitOfWork = unitOfWork;
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
            try
            {
                await UpsertBudgetAsync(client, entity, _budgetPushOmitsOptionalColumns);
            }
            catch (Exception ex) when (!_budgetPushOmitsOptionalColumns && IsMissingBudgetOptionalColumn(ex))
            {
                _logger.LogWarning(
                    "Supabase budgets table is missing icon/color/linked_account_id; pushing without them. {Error}",
                    ex.Message);
                _budgetPushOmitsOptionalColumns = true;
                await UpsertBudgetAsync(client, entity, true);
            }
        }

        _logger.LogDebug("Synced budget {BudgetId} ({OperationType})", entity.Id, operationType);
    }

    private static bool IsMissingBudgetOptionalColumn(Exception exception)
    {
        var match = MissingColumnRegex.Match(exception.ToString());
        return match.Success &&
               match.Groups["table"].Value.Equals("budgets", StringComparison.OrdinalIgnoreCase) &&
               BudgetOptionalColumns.Contains(match.Groups["column"].Value, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task UpsertBudgetAsync(global::Supabase.Client client, Budget entity, bool omitOptionalColumns)
    {
        if (omitOptionalColumns)
        {
            await client.From<BudgetRecordLite>().Upsert(new BudgetRecordLite
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
                UserId = entity.UserId,
                Icon = entity.Icon,
                Color = entity.Color,
                LinkedAccountId = entity.LinkedAccountId?.Value
            });
        }
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

    public async Task<int> PullAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var client = await _clientProvider.TryGetClientAsync(cancellationToken);
        if (client == null)
        {
            _logger.LogDebug("Skipping pull - Supabase not configured");
            return 0;
        }

        var merged = 0;
        merged += await PullAccountsAsync(client, userId, cancellationToken);
        merged += await PullCategoriesAsync(client, userId, cancellationToken);
        merged += await PullTransactionsAsync(client, userId, cancellationToken);
        merged += await PullBudgetsAsync(client, userId, cancellationToken);
        merged += await PullRecurringAsync(client, userId, cancellationToken);
        merged += await PullGoalsAsync(client, userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Pulled {Count} server rows for user {UserId}", merged, userId);
        return merged;
    }

    private static bool IsPending(SyncStatus status) =>
        status is SyncStatus.PendingCreate or SyncStatus.PendingUpdate or SyncStatus.PendingDelete;

    private async Task<int> PullAccountsAsync(global::Supabase.Client client, Guid userId, CancellationToken ct)
    {
        var response = await client.From<AccountRecord>().Where(r => r.UserId == userId).Get();
        var merged = 0;

        foreach (var record in response.Models)
        {
            if (record.Id == Guid.Empty)
                continue;

            try
            {
                var local = await _unitOfWork.Accounts.GetByIdAsync(record.Id, ct);
                if (local == null)
                {
                    var created = new Account(
                        record.Name,
                        Enum.Parse<AccountType>(record.Type),
                        new Money(record.BalanceAmount, record.BalanceCurrency),
                        record.UserId,
                        record.Description,
                        record.Icon,
                        record.Color,
                        record.IsDefault,
                        record.SortOrder);
                    // Adopt server identity for the freshly built row.
                    await _unitOfWork.Accounts.AddAsync(created, ct);
                    created.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    merged++;
                }
                else if (!IsPending(local.SyncStatus))
                {
                    if (record.IsDeleted)
                    {
                        local.MarkAsDeleted();
                        local.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, true);
                        await _unitOfWork.Accounts.UpdateAsync(local, ct);
                        merged++;
                    }
                    else if (record.UpdatedAt > local.UpdatedAt)
                    {
                        local.UpdateName(record.Name);
                        local.UpdateType(Enum.Parse<AccountType>(record.Type));
                        local.SetBalance(new Money(record.BalanceAmount, record.BalanceCurrency));
                        local.UpdateDescription(record.Description);
                        local.UpdateIcon(record.Icon);
                        local.UpdateColor(record.Color);
                        local.UpdateSortOrder(record.SortOrder);
                        if (record.IsDefault) local.SetAsDefault(); else local.UnsetAsDefault();
                        local.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, false);
                        await _unitOfWork.Accounts.UpdateAsync(local, ct);
                        merged++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping account row {Id} during pull", record.Id);
            }
        }

        return merged;
    }

    private async Task<int> PullCategoriesAsync(global::Supabase.Client client, Guid userId, CancellationToken ct)
    {
        var response = await client.From<CategoryRecord>().Where(r => r.UserId == userId).Get();
        var merged = 0;

        foreach (var record in response.Models)
        {
            if (record.Id == Guid.Empty)
                continue;

            try
            {
                var local = await _unitOfWork.Categories.GetByIdAsync(record.Id, ct);
                if (local == null)
                {
                    var created = new Category(
                        record.Name,
                        Enum.Parse<CategoryType>(record.Type),
                        record.UserId,
                        record.Icon,
                        record.Color,
                        record.ParentCategoryId,
                        record.IsSystem,
                        record.SortOrder);
                    await _unitOfWork.Categories.AddAsync(created, ct);
                    created.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    if (!record.IsActive) created.Deactivate();
                    created.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    merged++;
                }
                else if (!IsPending(local.SyncStatus))
                {
                    if (record.IsDeleted)
                    {
                        local.MarkAsDeleted();
                        local.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, true);
                        await _unitOfWork.Categories.UpdateAsync(local, ct);
                        merged++;
                    }
                    else if (record.UpdatedAt > local.UpdatedAt)
                    {
                        local.UpdateName(record.Name);
                        local.UpdateIcon(record.Icon);
                        local.UpdateColor(record.Color);
                        local.UpdateSortOrder(record.SortOrder);
                        local.SetParentCategory(record.ParentCategoryId);
                        if (record.IsActive) local.Activate(); else local.Deactivate();
                        local.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, false);
                        await _unitOfWork.Categories.UpdateAsync(local, ct);
                        merged++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping category row {Id} during pull", record.Id);
            }
        }

        return merged;
    }

    private async Task<int> PullTransactionsAsync(global::Supabase.Client client, Guid userId, CancellationToken ct)
    {
        var response = await client.From<TransactionRecord>().Where(r => r.UserId == userId).Get();
        var merged = 0;

        foreach (var record in response.Models)
        {
            if (record.Id == Guid.Empty)
                continue;

            try
            {
                var local = await _unitOfWork.Transactions.GetByIdAsync(record.Id, ct);
                if (local == null)
                {
                    var created = new Transaction(
                        Enum.Parse<TransactionType>(record.Type),
                        new Money(record.Amount, record.Currency),
                        record.Date,
                        new AccountId(record.AccountId),
                        new CategoryId(record.CategoryId),
                        record.UserId,
                        record.Notes,
                        record.RecurringTransactionId);
                    await _unitOfWork.Transactions.AddAsync(created, ct);
                    created.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    merged++;
                }
                else if (!IsPending(local.SyncStatus))
                {
                    if (record.IsDeleted)
                    {
                        local.MarkAsDeleted();
                        local.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, true);
                        await _unitOfWork.Transactions.UpdateAsync(local, ct);
                        merged++;
                    }
                    else if (record.UpdatedAt > local.UpdatedAt)
                    {
                        local.UpdateAmount(new Money(record.Amount, record.Currency));
                        local.UpdateDate(record.Date);
                        local.UpdateNotes(record.Notes);
                        local.UpdateAccount(new AccountId(record.AccountId));
                        local.UpdateCategory(new CategoryId(record.CategoryId));
                        local.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, false);
                        await _unitOfWork.Transactions.UpdateAsync(local, ct);
                        merged++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping transaction row {Id} during pull", record.Id);
            }
        }

        return merged;
    }

    private async Task<int> PullBudgetsAsync(global::Supabase.Client client, Guid userId, CancellationToken ct)
    {
        var response = await client.From<BudgetRecord>().Where(r => r.UserId == userId).Get();
        var merged = 0;

        foreach (var record in response.Models)
        {
            if (record.Id == Guid.Empty)
                continue;

            try
            {
                var local = await _unitOfWork.Budgets.GetByIdAsync(record.Id, ct);
                if (local == null)
                {
                    var created = new Budget(
                        record.Name,
                        new Money(record.Amount, record.Currency),
                        record.StartDate,
                        record.EndDate,
                        new CategoryId(record.CategoryId),
                        record.UserId,
                        null,
                        null);
                    await _unitOfWork.Budgets.AddAsync(created, ct);
                    created.SetSpentAmount(new Money(record.SpentAmount, record.Currency));
                    created.UpdateIcon(record.Icon);
                    created.UpdateColor(record.Color);
                    created.UpdateLinkedAccount(record.LinkedAccountId.HasValue ? new AccountId(record.LinkedAccountId.Value) : null);
                    created.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    merged++;
                }
                else if (!IsPending(local.SyncStatus))
                {
                    if (record.IsDeleted)
                    {
                        local.MarkAsDeleted();
                        local.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, true);
                        await _unitOfWork.Budgets.UpdateAsync(local, ct);
                        merged++;
                    }
                    else if (record.UpdatedAt > local.UpdatedAt)
                    {
                        local.UpdateName(record.Name);
                        local.UpdateAmount(new Money(record.Amount, record.Currency));
                        local.UpdateDates(record.StartDate, record.EndDate);
                        local.UpdateCategory(new CategoryId(record.CategoryId));
                        local.UpdateIcon(record.Icon);
                        local.UpdateColor(record.Color);
                        local.UpdateLinkedAccount(record.LinkedAccountId.HasValue ? new AccountId(record.LinkedAccountId.Value) : null);
                        local.SetSpentAmount(new Money(record.SpentAmount, record.Currency));
                        local.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, false);
                        await _unitOfWork.Budgets.UpdateAsync(local, ct);
                        merged++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping budget row {Id} during pull", record.Id);
            }
        }

        return merged;
    }

    private async Task<int> PullRecurringAsync(global::Supabase.Client client, Guid userId, CancellationToken ct)
    {
        var response = await client.From<RecurringTransactionRecord>().Where(r => r.UserId == userId).Get();
        var merged = 0;

        foreach (var record in response.Models)
        {
            if (record.Id == Guid.Empty)
                continue;

            try
            {
                var local = await _unitOfWork.RecurringTransactions.GetByIdAsync(record.Id, ct);
                if (local == null)
                {
                    var created = new RecurringTransaction(
                        record.Name,
                        Enum.Parse<TransactionType>(record.Type),
                        new Money(record.Amount, record.Currency),
                        Enum.Parse<RecurringFrequency>(record.Frequency),
                        record.StartDate,
                        new AccountId(record.AccountId),
                        new CategoryId(record.CategoryId),
                        record.UserId,
                        record.Notes,
                        record.EndDate);
                    await _unitOfWork.RecurringTransactions.AddAsync(created, ct);
                    if (!record.IsActive) created.Deactivate();
                    if (record.LastGeneratedAt.HasValue) created.RecordGeneration(record.LastGeneratedAt.Value);
                    created.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    merged++;
                }
                else if (!IsPending(local.SyncStatus))
                {
                    if (record.IsDeleted)
                    {
                        local.MarkAsDeleted();
                        local.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, true);
                        await _unitOfWork.RecurringTransactions.UpdateAsync(local, ct);
                        merged++;
                    }
                    else if (record.UpdatedAt > local.UpdatedAt)
                    {
                        local.UpdateName(record.Name);
                        local.UpdateAmount(new Money(record.Amount, record.Currency));
                        local.UpdateFrequency(Enum.Parse<RecurringFrequency>(record.Frequency));
                        local.UpdateDates(record.StartDate, record.EndDate);
                        local.UpdateAccount(new AccountId(record.AccountId));
                        local.UpdateCategory(new CategoryId(record.CategoryId));
                        local.UpdateNotes(record.Notes);
                        if (record.IsActive) local.Activate(); else local.Deactivate();
                        if (record.LastGeneratedAt.HasValue) local.RecordGeneration(record.LastGeneratedAt.Value);
                        local.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, false);
                        await _unitOfWork.RecurringTransactions.UpdateAsync(local, ct);
                        merged++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping recurring row {Id} during pull", record.Id);
            }
        }

        return merged;
    }

    private async Task<int> PullGoalsAsync(global::Supabase.Client client, Guid userId, CancellationToken ct)
    {
        var response = await client.From<FinancialGoalRecord>().Where(r => r.UserId == userId).Get();
        var merged = 0;

        foreach (var record in response.Models)
        {
            if (record.Id == Guid.Empty)
                continue;

            try
            {
                var local = await _unitOfWork.FinancialGoals.GetByIdAsync(record.Id, ct);
                if (local == null)
                {
                    var created = new FinancialGoal(
                        record.Name,
                        new Money(record.TargetAmount, record.TargetCurrency),
                        record.TargetDate,
                        record.UserId,
                        record.StartDate,
                        null,
                        record.Icon,
                        record.Color,
                        record.LinkedAccountId.HasValue ? new AccountId(record.LinkedAccountId.Value) : null);
                    await _unitOfWork.FinancialGoals.AddAsync(created, ct);
                    created.SetProgress(new Money(record.CurrentAmount, record.TargetCurrency));
                    created.SetStatus(Enum.Parse<GoalStatus>(record.Status));
                    created.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    merged++;
                }
                else if (!IsPending(local.SyncStatus))
                {
                    if (record.IsDeleted)
                    {
                        local.MarkAsDeleted();
                        local.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, true);
                        await _unitOfWork.FinancialGoals.UpdateAsync(local, ct);
                        merged++;
                    }
                    else if (record.UpdatedAt > local.UpdatedAt)
                    {
                        local.UpdateName(record.Name);
                        local.UpdateTargetAmount(new Money(record.TargetAmount, record.TargetCurrency));
                        local.UpdateTargetDate(record.TargetDate);
                        local.UpdateDescription(null);
                        local.UpdateIcon(record.Icon);
                        local.UpdateColor(record.Color);
                        local.UpdateLinkedAccount(record.LinkedAccountId.HasValue ? new AccountId(record.LinkedAccountId.Value) : null);
                        local.SetProgress(new Money(record.CurrentAmount, record.TargetCurrency));
                        local.SetStatus(Enum.Parse<GoalStatus>(record.Status));
                        local.AdoptRemoteState(record.CreatedAt, record.UpdatedAt, record.Version, false);
                        merged += 1;
                        await _unitOfWork.FinancialGoals.UpdateAsync(local, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping goal row {Id} during pull", record.Id);
            }
        }

        return merged;
    }
}
