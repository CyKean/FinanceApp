namespace FinanceApp.Application.Services;

using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

/// <summary>
/// Repairs rows that exist twice because an older build could not recognise a
/// server row as the one already on the device.
/// <para>
/// The pull used to build a fresh entity for every server row and then keep the
/// new client-minted id instead of the row's own, so nothing could ever be
/// matched again: the copy stayed, the next sync pushed it back up beside the
/// original, and every following sync added another. Devices that ran that build
/// are left holding two (or more) rows for one real account, transaction or
/// category, and the server often holds them too.
/// </para>
/// <para>
/// Rows are matched on what they are rather than on their id: an account is the
/// same account when its name, type and creation time agree. Creation time is
/// the part that keeps a user who deliberately made two "Cash" wallets apart from
/// a copy of one - the copy inherits the original's creation time from the server
/// row it came from, and two accounts the user really did create do not share an
/// instant. The window is a second rather than a tick because the value survives
/// a round trip through a database with microsecond precision.
/// </para>
/// <para>
/// The row that is kept is decided by its content, not by anything this device
/// happens to hold: the older of the two, with the id as the tie-break. Every
/// device holding both rows evaluates that identically, so two devices merge to
/// the same survivor - choosing by what the local data points at would let one
/// device keep its copy, delete the other's, and the next sync undo both.
/// Everything that referenced a retired row is moved onto the survivor first:
/// the push that removes it would otherwise be refused by the server, which will
/// not drop a row something still references.
/// </para>
/// </summary>
public class SyncDeduplicationService
{
    /// <summary>
    /// How far apart two creation times may be and still be the same instant.
    /// Wide enough for the microsecond a server drops off the value, narrow
    /// enough that two records a user really made twice never land inside it.
    /// </summary>
    private static readonly TimeSpan TwinWindow = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Default categories are named, not stamped: two devices each open an
    /// account, each make their own copy of "Food", and the two copies are the
    /// same category by construction however far apart they were made. The name
    /// is the identity, so no window is applied to them.
    /// </summary>
    private static readonly TimeSpan NoWindow = TimeSpan.MaxValue;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _logger;

    public SyncDeduplicationService(IUnitOfWork unitOfWork, ILogger logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Merges every duplicated row of <paramref name="userId"/> into one and
    /// queues the deletions that remove the spares from the server. Returns the
    /// number of rows merged away; zero is the normal case and costs three
    /// grouped reads.
    /// </summary>
    public async Task<int> DeduplicateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var accounts = await _unitOfWork.Accounts.GetByUserIdAsync(userId, cancellationToken);
        var categories = await _unitOfWork.Categories.GetByUserIdAsync(userId, cancellationToken);
        var transactions = await _unitOfWork.Transactions.GetByUserIdAsync(userId, cancellationToken);

        var accountTwins = Clusters(
            accounts,
            a => (a.Name, a.Type),
            a => a.CreatedAt,
            _ => TwinWindow);
        var categoryTwins = Clusters(
            categories,
            c => (c.Name, c.Type, c.IsSystem),
            c => c.CreatedAt,
            c => c.IsSystem ? NoWindow : TwinWindow);

        var merged = 0;
        var changed = false;

        if (accountTwins.Count > 0)
        {
            var budgets = await _unitOfWork.Budgets.GetByUserIdAsync(userId, cancellationToken);
            var recurring = await _unitOfWork.RecurringTransactions.GetByUserIdAsync(userId, cancellationToken);
            var goals = await _unitOfWork.FinancialGoals.GetByUserIdAsync(userId, cancellationToken);

            foreach (var cluster in accountTwins)
            {
                var survivor = PickSurvivor(cluster);
                foreach (var spare in cluster.Where(a => a.Id != survivor.Id))
                {
                    foreach (var transaction in transactions.Where(t => t.AccountId.Value == spare.Id))
                        transaction.UpdateAccount(new AccountId(survivor.Id));

                    foreach (var budget in budgets.Where(b => b.LinkedAccountId?.Value == spare.Id))
                        budget.UpdateLinkedAccount(new AccountId(survivor.Id));

                    foreach (var goal in goals.Where(g => g.LinkedAccountId?.Value == spare.Id))
                        goal.UpdateLinkedAccount(new AccountId(survivor.Id));

                    foreach (var plan in recurring.Where(r => r.AccountId.Value == spare.Id))
                        plan.UpdateAccount(new AccountId(survivor.Id));

                    Retire(spare);
                    merged++;
                    changed = true;
                    _logger.LogInformation(
                        "Merged duplicate account {SpareId} ({Name}) into {SurvivorId}",
                        spare.Id, spare.Name, survivor.Id);
                }
            }
        }

        if (categoryTwins.Count > 0)
        {
            var budgets = await _unitOfWork.Budgets.GetByUserIdAsync(userId, cancellationToken);
            var recurring = await _unitOfWork.RecurringTransactions.GetByUserIdAsync(userId, cancellationToken);

            foreach (var cluster in categoryTwins)
            {
                var survivor = PickSurvivor(cluster);
                foreach (var spare in cluster.Where(c => c.Id != survivor.Id))
                {
                    foreach (var transaction in transactions.Where(t => t.CategoryId.Value == spare.Id))
                        transaction.UpdateCategory(new CategoryId(survivor.Id));

                    foreach (var budget in budgets.Where(b => b.CategoryId.Value == spare.Id))
                        budget.UpdateCategory(new CategoryId(survivor.Id));

                    foreach (var plan in recurring.Where(r => r.CategoryId.Value == spare.Id))
                        plan.UpdateCategory(new CategoryId(survivor.Id));

                    foreach (var child in categories.Where(c => c.ParentCategoryId == spare.Id))
                    {
                        child.SetParentCategory(survivor.Id);
                        child.MarkAsPendingUpdate();
                    }

                    Retire(spare);
                    merged++;
                    changed = true;
                    _logger.LogInformation(
                        "Merged duplicate category {SpareId} ({Name}) into {SurvivorId}",
                        spare.Id, spare.Name, survivor.Id);
                }
            }
        }

        // After the links above, duplicates of the same expense meet for the
        // first time: two copies that pointed at two copies of the account are
        // now indistinguishable, which is what makes them safe to collapse.
        foreach (var cluster in Clusters(
            transactions,
            t => (t.Type, t.Amount.Amount, t.Amount.Currency, t.Date, t.AccountId.Value, t.CategoryId.Value, t.Notes),
            t => t.CreatedAt,
            _ => TwinWindow))
        {
            var survivor = PickSurvivor(cluster);

            foreach (var spare in cluster.Where(t => t.Id != survivor.Id))
            {
                Retire(spare);
                merged++;
                changed = true;
                _logger.LogInformation(
                    "Merged duplicate transaction {SpareId} into {SurvivorId}",
                    spare.Id, survivor.Id);
            }
        }

        // A merge can retire the account that was badged the default, and the
        // app needs exactly one. The pull demotes a second default but has
        // nothing to promote when none is left, which is this side's job.
        if (changed && !accounts.Any(a => !a.IsDeleted && a.IsDefault))
        {
            var successor = accounts.FirstOrDefault(a => !a.IsDeleted);
            if (successor != null)
            {
                successor.SetAsDefault();
                successor.MarkAsPendingUpdate();
            }
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Repaired {Merged} duplicate rows for user {UserId}", merged, userId);
        }

        return merged;
    }

    /// <summary>
    /// Groups rows that describe the same thing: same natural key, and creation
    /// times close enough to be the same instant. Consecutive rows chain, so a
    /// group of three made in quick succession collapses as one cluster rather
    /// than as three pairs.
    /// </summary>
    private static List<List<T>> Clusters<T>(
        IEnumerable<T> items,
        Func<T, object> naturalKey,
        Func<T, DateTime> createdAt,
        Func<T, TimeSpan> window)
    {
        var clusters = new List<List<T>>();

        foreach (var group in items.GroupBy(naturalKey))
        {
            var ordered = group.OrderBy(createdAt).ToList();
            var current = new List<T> { ordered[0] };

            for (var i = 1; i < ordered.Count; i++)
            {
                if (createdAt(ordered[i]) - createdAt(current[^1]) <= window(ordered[i]))
                {
                    current.Add(ordered[i]);
                    continue;
                }

                if (current.Count > 1)
                    clusters.Add(current);

                current = new List<T> { ordered[i] };
            }

            if (current.Count > 1)
                clusters.Add(current);
        }

        return clusters;
    }

    /// <summary>
    /// The row to keep: the older one, with the id as the tie-break. Both halves
    /// come from the rows themselves, so two devices that hold the same twins
    /// reach the same answer without comparing notes - which is what stops the
    /// merge on one device undoing the merge on another.
    /// </summary>
    private static T PickSurvivor<T>(IEnumerable<T> cluster) where T : Entity =>
        cluster.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).First();

    /// <summary>
    /// Takes a row out of the app and out of the server. Pending-delete rather
    /// than a bare tombstone: the outbox only queues a deletion for a row whose
    /// status says so, and without it the spare would be pushed as a create on
    /// the next sync - the very duplication this repairs.
    /// </summary>
    private static void Retire(Entity entity)
    {
        entity.MarkAsPendingDelete();
        entity.MarkAsDeleted();
    }
}
