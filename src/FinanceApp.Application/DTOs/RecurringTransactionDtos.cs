namespace FinanceApp.Application.DTOs;

using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public record RecurringTransactionDto(
    Guid Id,
    string Name,
    TransactionType Type,
    Money Amount,
    RecurringFrequency Frequency,
    DateTime StartDate,
    DateTime? EndDate,
    AccountId AccountId,
    string AccountName,
    CategoryId CategoryId,
    string CategoryName,
    DateTime? LastGeneratedAt,
    DateTime? NextDueDate,
    bool IsActive,
    string? Notes,
    SyncStatus SyncStatus,
    DateTime? LastSyncedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDeleted
);

public record CreateRecurringTransactionDto(
    string Name,
    TransactionType Type,
    Money Amount,
    RecurringFrequency Frequency,
    DateTime StartDate,
    AccountId AccountId,
    CategoryId CategoryId,
    string? Notes = null,
    DateTime? EndDate = null
);

public record UpdateRecurringTransactionDto(
    string? Name = null,
    Money? Amount = null,
    RecurringFrequency? Frequency = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    AccountId? AccountId = null,
    CategoryId? CategoryId = null,
    string? Notes = null,
    bool? IsActive = null
);