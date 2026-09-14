namespace FinanceApp.Infrastructure.Repositories;

using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class FinancialGoalRepository : BaseRepository<FinancialGoal>, IFinancialGoalRepository
{
    public FinancialGoalRepository(FinanceAppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<FinancialGoal>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialGoal>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(g => g.UserId == userId && g.Status == GoalStatus.Active)
            .OrderBy(g => g.TargetDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialGoal>> GetByStatusAsync(Guid userId, GoalStatus status, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(g => g.UserId == userId && g.Status == status)
            .OrderBy(g => g.TargetDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialGoal>> GetPendingSyncAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(g => g.UserId == userId && g.SyncStatus != SyncStatus.Synced)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}