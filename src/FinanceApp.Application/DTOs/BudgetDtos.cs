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
    bool IsOverBudget,
    bool IsNearLimit,
    SyncStatus SyncStatus,
    DateTime? LastSyncedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDeleted
);

public record CreateBudgetDto(
    string Name,
    Money Amount,
    DateTime StartDate,
    DateTime EndDate,
    CategoryId CategoryId
);

public record UpdateBudgetDto(
    string? Name = null,
    Money? Amount = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    CategoryId? CategoryId = null
);