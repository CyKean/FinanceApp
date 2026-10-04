namespace FinanceApp.Infrastructure.Repositories;

using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class AccountRepository : BaseRepository<Account>, IAccountRepository
{
    public AccountRepository(FinanceAppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Account>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Account?> GetDefaultAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault, cancellationToken);
    }

    public async Task<IReadOnlyList<Account>> GetByTypeAsync(Guid userId, AccountType type, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(a => a.UserId == userId && a.Type == type)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Money> GetTotalBalanceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var total = await DbSet
            .Where(a => a.UserId == userId)
            .SumAsync(a => a.Balance.Amount, cancellationToken);

        return new Money(total, "PHP");
    }

    public async Task<int> EnsureSingleDefaultAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var defaults = await DbSet
            .Where(a => a.UserId == userId && a.IsDefault)
            .OrderByDescending(a => a.UpdatedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        // Everything but the newest is demoted. Pending, so the correction travels
        // to the other devices instead of being rediscovered on each of them.
        foreach (var account in defaults.Skip(1))
        {
            account.UnsetAsDefault();
            account.MarkAsPendingUpdate();
            await UpdateAsync(account, cancellationToken);
        }

        return Math.Max(0, defaults.Count - 1);
    }

    public async Task<IReadOnlyDictionary<Guid, Account>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, Account>();

        return await DbSet
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);
    }
}