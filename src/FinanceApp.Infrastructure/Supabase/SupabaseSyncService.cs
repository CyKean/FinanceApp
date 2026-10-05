namespace FinanceApp.Infrastructure.Supabase;

using System.Text.RegularExpressions;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
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
    /// <summary>
    /// The two shapes PostgREST uses to report a column the table does not have:
    /// the schema-cache complaint it raises while resolving a select, and the one
    /// Postgres raises when an upsert names a column that is not there. .NET lets
    /// both alternatives write into the same group names, so callers need not care
    /// which one arrived.
    /// </summary>
    private static readonly Regex MissingColumnRegex = new(
        @"Could not find the '(?<column>[^']+)' column of '(?<table>[^']+)'|column ""(?<column>[^""]+)"" of relation ""(?<table>[^""]+)"" does not exist",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] BudgetOptionalColumns = { "icon", "color", "linked_account_id" };

    private static readonly string[] AccountOptionalColumns = { "initial_balance_amount" };

    private readonly SupabaseClientProvider _clientProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountBalanceService _accountBalanceService;
    private readonly SyncDeduplicationService _deduplication;
    private readonly ILogger<SupabaseSyncService> _logger;
    private volatile bool _budgetPushOmitsOptionalColumns;
    private volatile bool _accountsUseLegacyShape;

    public SupabaseSyncService(
        SupabaseClientProvider clientProvider,
        IUnitOfWork unitOfWork,
        IAccountBalanceService accountBalanceService,
        ILogger<SupabaseSyncService> logger)
    {
        _clientProvider = clientProvider;
        _unitOfWork = unitOfWork;
        _accountBalanceService = accountBalanceService;
        _logger = logger;
        // Built here rather than injected: it shares this service's unit of work
        // and needs no interface of its own - it is a repair pass over the rows
        // the pull has just merged, not a service the app calls.
        _deduplication = new SyncDeduplicationService(unitOfWork, logger);
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
            try
            {
                await UpsertAccountAsync(client, entity, _accountsUseLegacyShape);
            }
            catch (Exception ex) when (!_accountsUseLegacyShape && IsMissingColumn(ex, "accounts", AccountOptionalColumns))
            {
                _logger.LogWarning(
                    "Supabase accounts table is missing initial_balance_amount; pushing without them. {Error}",
                    ex.Message);
                _accountsUseLegacyShape = true;   // sticky for process lifetime
                await UpsertAccountAsync(client, entity, true);
            }
        }

        _logger.LogDebug("Synced account {AccountId} ({OperationType})", entity.Id, operationType);
    }

    private static async Task UpsertAccountAsync(global::Supabase.Client client, Account entity, bool omitInitialBalance)
    {
        var common = new
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
        };

        if (omitInitialBalance)
        {
            await client.From<AccountRecordLite>().Upsert(new AccountRecordLite
            {
                Id = common.Id,
                CreatedAt = common.CreatedAt,
                UpdatedAt = common.UpdatedAt,
                IsDeleted = common.IsDeleted,
                Version = common.Version,
                Name = common.Name,
                Type = common.Type,
                BalanceAmount = common.BalanceAmount,
                BalanceCurrency = common.BalanceCurrency,
                Description = common.Description,
                Icon = common.Icon,
                Color = common.Color,
                UserId = common.UserId,
                IsDefault = common.IsDefault,
                SortOrder = common.SortOrder
            });
            return;
        }

        await client.From<AccountRecord>().Upsert(new AccountRecord
        {
            Id = common.Id,
            CreatedAt = common.CreatedAt,
            UpdatedAt = common.UpdatedAt,
            IsDeleted = common.IsDeleted,
            Version = common.Version,
            Name = common.Name,
            Type = common.Type,
            BalanceAmount = common.BalanceAmount,
            BalanceCurrency = common.BalanceCurrency,
            InitialBalanceAmount = entity.InitialBalanceAmount,
            Description = common.Description,
            Icon = common.Icon,
            Color = common.Color,
            UserId = common.UserId,
            IsDefault = common.IsDefault,
            SortOrder = common.SortOrder
        });
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

    private static bool IsMissingBudgetOptionalColumn(Exception exception) =>
        IsMissingColumn(exception, "budgets", BudgetOptionalColumns);

    /// <summary>
    /// True when PostgREST rejected the request because the table lacks one of
    /// the given columns, which is how a project that predates a migration is
    /// detected without a version handshake.
    /// </summary>
    private static bool IsMissingColumn(Exception exception, string table, string[] columns)
    {
        var match = MissingColumnRegex.Match(exception.ToString());
        return match.Success &&
               match.Groups["table"].Value.Equals(table, StringComparison.OrdinalIgnoreCase) &&
               columns.Contains(match.Groups["column"].Value, StringComparer.OrdinalIgnoreCase);
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

        // Each table pulls independently: one missing/blocked table must not
        // abort the other five.
        var merged = 0;
        Exception? lastError = null;

        merged += await TryPullAsync("accounts", () => PullAccountsAsync(client, userId, cancellationToken), e => lastError = e);
        merged += await TryPullAsync("categories", () => PullCategoriesAsync(client, userId, cancellationToken), e => lastError = e);
        merged += await TryPullAsync("transactions", () => PullTransactionsAsync(client, userId, cancellationToken), e => lastError = e);
        merged += await TryPullAsync("budgets", () => PullBudgetsAsync(client, userId, cancellationToken), e => lastError = e);
        merged += await TryPullAsync("recurring", () => PullRecurringAsync(client, userId, cancellationToken), e => lastError = e);
        merged += await TryPullAsync("goals", () => PullGoalsAsync(client, userId, cancellationToken), e => lastError = e);

        // Two devices can each hold an account badged as the default - one made
        // here before any of the user's data arrived, one arriving from the server
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (merged == 0 && lastError != null)
            throw lastError;

        // Rows left duplicated by the build this replaces are repaired now that
        // they are all here: the pull is the only moment both copies of an
        // account are in one database, and the deletions it queues are pushed by
        // the caller's second pass, after this.
        await RepairDuplicatesAsync(userId, cancellationToken);

        // The transaction table has just been merged, and a transaction carries
        // no balance of its own - the balances come from adding it up per
        // account. Without this step an account that gained a transaction on
        // another device would keep the balance it had before the pull, which is
        // how a balance used to reappear at its pre-sync figure.
        await _accountBalanceService.RecalculateAllAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Two devices can each hold an account badged as the default - one made
        // here before any of the user's data arrived, one arriving from the server
        // already flagged - and the app needs exactly one. After the saves, so the
        // check sees the rows this pull just inserted: a query runs against the
        // database, not the pending changes.
        var demoted = await _unitOfWork.Accounts.EnsureSingleDefaultAsync(userId, cancellationToken);
        if (demoted > 0)
        {
            _logger.LogInformation("Demoted {Count} duplicate default account(s) for user {UserId}", demoted, userId);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Pulled {Count} server rows for user {UserId}", merged, userId);
        return merged;
    }

    private async Task<int> TryPullAsync(string table, Func<Task<int>> pull, Action<Exception> onError)
    {
        try
        {
            return await pull();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pull failed for table {Table}, continuing with the rest", table);
            onError(ex);
            return 0;
        }
    }

    /// <summary>
    /// Merges the rows an older build left duplicated. A repair rather than the
    /// sync itself, so a bad row costs the repair and not the pull: the six
    /// tables have already been merged and are worth keeping.
    /// </summary>
    private async Task RepairDuplicatesAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var repaired = await _deduplication.DeduplicateAsync(userId, cancellationToken);
            if (repaired > 0)
                _logger.LogInformation("Merged {Repaired} duplicate rows while pulling for user {UserId}", repaired, userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Duplicate repair failed for user {UserId}; leaving the rows as they are", userId);
        }
    }

    private static bool IsPending(SyncStatus status) =>
        status is SyncStatus.PendingCreate or SyncStatus.PendingUpdate or SyncStatus.PendingDelete;

    /// <summary>
    /// The opening balance a pulled account row carries.
    /// <para>
    /// A project that predates migration 0003 has no such column, and the select
    /// asks for whatever the table has rather than failing: the property simply
    /// arrives unset. Reading it as zero would give every account an opening
    /// balance of zero, and the recalculation at the end of the pull would then
    /// write that over the balance the user was looking at - the original report,
    /// by another route. The figure the server does hold is the balance itself,
    /// which older builds never managed to update (the nudge was dropped once the
    /// row had synced), so for them it is still the opening balance.
    /// </para>
    /// </summary>
    private decimal InitialBalanceOf(AccountRecord record)
    {
        if (record.InitialBalanceAmount is { } initial)
            return initial;

        if (!_accountsUseLegacyShape)
        {
            // Sticky for the process: every later push of an account drops the
            // column too, rather than failing on it once per sync.
            _accountsUseLegacyShape = true;
            _logger.LogWarning(
                "Supabase accounts table has no initial_balance_amount (migration 0003 not applied); " +
                "taking the opening balance from balance_amount and pushing accounts without the column");
        }

        return record.BalanceAmount;
    }

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
                    // The opening balance comes across so this device derives the
                    // same balance the other one did. Starting from the server's
                    // cached balance instead would count the transactions that
                    // are about to be pulled a second time.
                    var initialBalance = new Money(InitialBalanceOf(record), record.BalanceCurrency);

                    var created = new Account(
                        record.Name,
                        Enum.Parse<AccountType>(record.Type),
                        initialBalance,
                        record.UserId,
                        record.Description,
                        record.Icon,
                        record.Color,
                        record.IsDefault,
                        record.SortOrder);
                    // Adopt server identity for the freshly built row.
                    await _unitOfWork.Accounts.AddAsync(created, ct);
                    created.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    merged++;
                }
                else if (!IsPending(local.SyncStatus))
                {
                    if (record.IsDeleted)
                    {
                        local.MarkAsDeleted();
                        local.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, true);
                        await _unitOfWork.Accounts.UpdateAsync(local, ct);
                        merged++;
                    }
                    else if (record.UpdatedAt > local.UpdatedAt)
                    {
                        local.UpdateName(record.Name);
                        local.UpdateType(Enum.Parse<AccountType>(record.Type));
                        // Deliberately not the server's balance column: it is a
                        // cache of "opening balance plus transactions", and the
                        // transactions are merged separately below, so adopting
                        // it here is what put a stale figure back after a sync.
                        // The balance is derived from the transactions once the
                        // pull has finished instead.
                        local.UpdateDescription(record.Description);
                        local.UpdateIcon(record.Icon);
                        local.UpdateColor(record.Color);
                        local.UpdateSortOrder(record.SortOrder);
                        if (record.IsDefault) local.SetAsDefault(); else local.UnsetAsDefault();
                        local.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, false);
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
                    created.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    if (!record.IsActive) created.Deactivate();
                    created.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    merged++;
                }
                else if (!IsPending(local.SyncStatus))
                {
                    if (record.IsDeleted)
                    {
                        local.MarkAsDeleted();
                        local.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, true);
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
                        local.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, false);
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
                    created.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    merged++;
                }
                else if (!IsPending(local.SyncStatus))
                {
                    if (record.IsDeleted)
                    {
                        local.MarkAsDeleted();
                        local.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, true);
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
                        local.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, false);
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
                    created.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    merged++;
                }
                else if (!IsPending(local.SyncStatus))
                {
                    if (record.IsDeleted)
                    {
                        local.MarkAsDeleted();
                        local.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, true);
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
                        local.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, false);
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
                    created.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    merged++;
                }
                else if (!IsPending(local.SyncStatus))
                {
                    if (record.IsDeleted)
                    {
                        local.MarkAsDeleted();
                        local.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, true);
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
                        local.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, false);
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
                    created.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, record.IsDeleted);
                    merged++;
                }
                else if (!IsPending(local.SyncStatus))
                {
                    if (record.IsDeleted)
                    {
                        local.MarkAsDeleted();
                        local.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, true);
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
                        local.AdoptRemoteState(record.Id, record.CreatedAt, record.UpdatedAt, record.Version, false);
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
