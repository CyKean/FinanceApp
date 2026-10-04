namespace FinanceApp.Domain.Interfaces;

using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;

public interface ICategoryRepository : IRepository<Category>
{
    Task<IReadOnlyList<Category>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetByTypeAsync(Guid userId, CategoryType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetActiveByTypeAsync(Guid userId, CategoryType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetSystemCategoriesAsync(CategoryType type, CancellationToken cancellationToken = default);
    Task<Category?> GetByNameAsync(Guid userId, string name, CategoryType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch lookup for lookups inside a loop. Fetching by id one at a time is
    /// what turned mapping 10 transactions into 20 round-trips.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Category>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}