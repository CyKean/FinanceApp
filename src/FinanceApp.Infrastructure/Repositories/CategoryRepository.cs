namespace FinanceApp.Infrastructure.Repositories;

using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class CategoryRepository : BaseRepository<Category>, ICategoryRepository
{
    public CategoryRepository(FinanceAppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Category>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetByTypeAsync(Guid userId, CategoryType type, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(c => c.UserId == userId && c.Type == type)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetActiveByTypeAsync(Guid userId, CategoryType type, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(c => c.UserId == userId && c.Type == type && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetSystemCategoriesAsync(CategoryType type, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(c => c.IsSystem && c.Type == type)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Category?> GetByNameAsync(Guid userId, string name, CategoryType type, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == name && c.Type == type, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, Category>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, Category>();

        // No tracking: these are read purely to fill in names for a DTO, and the
        // shared context would otherwise hold every one of them for the life of
        // the process.
        return await DbSet
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);
    }
}