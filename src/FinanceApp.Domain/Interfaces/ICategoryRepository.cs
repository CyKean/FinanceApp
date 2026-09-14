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
}