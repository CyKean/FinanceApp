namespace FinanceApp.Application.DTOs;

using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public record TransactionDto(
    Guid Id,
    TransactionType Type,
    Money Amount,
    DateTime Date,
    string? Notes,
    AccountId AccountId,
    string AccountName,
    CategoryId CategoryId,
    string CategoryName,
    string CategoryIcon,
    string CategoryColor,
    Guid? RecurringTransactionId,
    SyncStatus SyncStatus,
    DateTime? LastSyncedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDeleted
);

public record CreateTransactionDto(
    TransactionType Type,
    Money Amount,
    DateTime Date,
    AccountId AccountId,
    CategoryId CategoryId,
    string? Notes = null,
    Guid? RecurringTransactionId = null
);

public record UpdateTransactionDto(
    Money? Amount = null,
    DateTime? Date = null,
    string? Notes = null,
    AccountId? AccountId = null,
    CategoryId? CategoryId = null,
    TransactionType? Type = null
);

public record TransactionFilterDto(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    TransactionType? Type = null,
    AccountId? AccountId = null,
    CategoryId? CategoryId = null,
    int Page = 1,
    int PageSize = 50
);

public record TransactionSummaryDto(
    Money TotalIncome,
    Money TotalExpense,
    Money NetAmount,
    int TransactionCount
);