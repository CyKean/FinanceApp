namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.DTOs;
using FinanceApp.Domain.ValueObjects;

public interface ITransactionService
{
    Task<TransactionDto> CreateAsync(CreateTransactionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<TransactionDto> UpdateAsync(Guid id, UpdateTransactionDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<TransactionDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionDto>> GetAllAsync(Guid userId, TransactionFilterDto filter, CancellationToken cancellationToken = default);
    Task<TransactionSummaryDto> GetSummaryAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionDto>> GetRecentAsync(Guid userId, int count, CancellationToken cancellationToken = default);
    Task<Money> GetTotalByCategoryAsync(Guid userId, CategoryId categoryId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionDto>> GetByAccountAsync(Guid userId, AccountId accountId, CancellationToken cancellationToken = default);
}