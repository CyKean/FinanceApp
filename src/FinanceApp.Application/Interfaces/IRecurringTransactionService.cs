namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.DTOs;

public interface IRecurringTransactionService
{
    Task<RecurringTransactionDto> CreateAsync(CreateRecurringTransactionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<RecurringTransactionDto> UpdateAsync(Guid id, UpdateRecurringTransactionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<RecurringTransactionDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringTransactionDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringTransactionDto>> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringTransactionDto>> GetDueAsync(Guid userId, DateTime asOfDate, CancellationToken cancellationToken = default);
    Task ProcessDueTransactionsAsync(Guid userId, DateTime asOfDate, CancellationToken cancellationToken = default);
    Task ActivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}