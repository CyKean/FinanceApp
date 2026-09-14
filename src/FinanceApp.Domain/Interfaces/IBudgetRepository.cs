namespace FinanceApp.Domain.Interfaces;

using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public interface IBudgetRepository : IRepository<Budget>
{
    Task<IReadOnlyList<Budget>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Budget>> GetActiveByUserIdAsync(Guid userId, DateTime asOfDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Budget>> GetByCategoryIdAsync(Guid userId, CategoryId categoryId, CancellationToken cancellationToken = default);
    Task<Budget?> GetActiveForCategoryAsync(Guid userId, CategoryId categoryId, DateTime asOfDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Budget>> GetPendingSyncAsync(Guid userId, CancellationToken cancellationToken = default);
}