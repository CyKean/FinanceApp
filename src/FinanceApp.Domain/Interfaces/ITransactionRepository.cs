namespace FinanceApp.Domain.Interfaces;

using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<IReadOnlyList<Transaction>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid userId, AccountId accountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByCategoryIdAsync(Guid userId, CategoryId categoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByTypeAsync(Guid userId, TransactionType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByTypeAndDateRangeAsync(Guid userId, TransactionType type, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<Money> GetTotalByTypeAsync(Guid userId, TransactionType type, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<Money> GetTotalByCategoryAsync(Guid userId, CategoryId categoryId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetPendingSyncAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetRecentAsync(Guid userId, int count, CancellationToken cancellationToken = default);
}