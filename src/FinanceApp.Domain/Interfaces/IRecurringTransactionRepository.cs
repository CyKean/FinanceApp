namespace FinanceApp.Domain.Interfaces;

using FinanceApp.Domain.Entities;

public interface IRecurringTransactionRepository : IRepository<RecurringTransaction>
{
    Task<IReadOnlyList<RecurringTransaction>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringTransaction>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringTransaction>> GetDueTransactionsAsync(Guid userId, DateTime asOfDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringTransaction>> GetPendingSyncAsync(Guid userId, CancellationToken cancellationToken = default);
}