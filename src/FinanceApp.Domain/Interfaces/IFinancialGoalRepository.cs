namespace FinanceApp.Domain.Interfaces;

using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;

public interface IFinancialGoalRepository : IRepository<FinancialGoal>
{
    Task<IReadOnlyList<FinancialGoal>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialGoal>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialGoal>> GetByStatusAsync(Guid userId, GoalStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialGoal>> GetPendingSyncAsync(Guid userId, CancellationToken cancellationToken = default);
}