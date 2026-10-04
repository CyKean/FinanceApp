namespace FinanceApp.Domain.Interfaces;

using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public interface IAccountRepository : IRepository<Account>
{
    Task<IReadOnlyList<Account>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Account?> GetDefaultAccountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Account>> GetByTypeAsync(Guid userId, AccountType type, CancellationToken cancellationToken = default);
    Task<Money> GetTotalBalanceAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Leaves the user with exactly one default account, keeping the most recently
    /// changed one. Merging devices can otherwise leave two rows badged Default -
    /// one created here before any of the user's data arrived, one arriving from
    /// the server already flagged - which makes "the default account" ambiguous.
    /// Returns how many had to be unset.
    /// </summary>
    Task<int> EnsureSingleDefaultAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Batch lookup, so resolving names for a list costs one query.</summary>
    Task<IReadOnlyDictionary<Guid, Account>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}