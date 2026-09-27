namespace FinanceApp.Application.DTOs;

using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public record BudgetDto(
    Guid Id,
    string Name,
    Money Amount,
    Money SpentAmount,
    Money RemainingAmount,
    decimal PercentageUsed,
    DateTime StartDate,
    DateTime EndDate,
    CategoryId CategoryId,
    string CategoryName,
    string CategoryIcon,
    string CategoryColor,
    string? Icon,
    string? Color,
    bool IsOverBudget,
    bool IsNearLimit,
    SyncStatus SyncStatus,
    DateTime? LastSyncedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDeleted,
    Guid? LinkedAccountId
);

public record CreateBudgetDto(
    string Name,
    Money Amount,
    DateTime StartDate,
    DateTime EndDate,
    CategoryId CategoryId,
    string? Icon = null,
    string? Color = null,
    AccountId? LinkedAccountId = null
);

public record UpdateBudgetDto(
    string? Name = null,
    Money? Amount = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    CategoryId? CategoryId = null,
    string? Icon = null,
    string? Color = null,
    AccountId? LinkedAccountId = null
);