namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.DTOs;
using FinanceApp.Domain.ValueObjects;

public interface IFinancialGoalService
{
    Task<FinancialGoalDto> CreateAsync(CreateFinancialGoalDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<FinancialGoalDto> UpdateAsync(Guid id, UpdateFinancialGoalDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<FinancialGoalDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialGoalDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialGoalDto>> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddProgressAsync(Guid id, GoalProgressDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task RemoveProgressAsync(Guid id, Money amount, Guid userId, CancellationToken cancellationToken = default);
    Task CompleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task PauseAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task ReactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}