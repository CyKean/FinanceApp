namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.DTOs;
using FinanceApp.Domain.ValueObjects;

public interface IBudgetService
{
    Task<BudgetDto> CreateAsync(CreateBudgetDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<BudgetDto> UpdateAsync(Guid id, UpdateBudgetDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<BudgetDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BudgetDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BudgetDto>> GetActiveAsync(Guid userId, DateTime asOfDate, CancellationToken cancellationToken = default);
    Task<BudgetDto?> GetActiveForCategoryAsync(Guid userId, CategoryId categoryId, DateTime asOfDate, CancellationToken cancellationToken = default);
    Task AddSpendingAsync(Guid budgetId, Money amount, Guid userId, CancellationToken cancellationToken = default);
    Task RemoveSpendingAsync(Guid budgetId, Money amount, Guid userId, CancellationToken cancellationToken = default);
}