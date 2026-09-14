namespace FinanceApp.Application.DTOs;

using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public record FinancialGoalDto(
    Guid Id,
    string Name,
    Money TargetAmount,
    Money CurrentAmount,
    Money RemainingAmount,
    decimal ProgressPercentage,
    DateTime TargetDate,
    DateTime StartDate,
    GoalStatus Status,
    string? Description,
    string? Icon,
    string? Color,
    AccountId? LinkedAccountId,
    string? LinkedAccountName,
    int DaysRemaining,
    Money RequiredMonthlySavings,
    bool IsOnTrack,
    SyncStatus SyncStatus,
    DateTime? LastSyncedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDeleted
);

public record CreateFinancialGoalDto(
    string Name,
    Money TargetAmount,
    DateTime TargetDate,
    DateTime? StartDate = null,
    string? Description = null,
    string? Icon = null,
    string? Color = null,
    AccountId? LinkedAccountId = null
);

public record UpdateFinancialGoalDto(
    string? Name = null,
    Money? TargetAmount = null,
    DateTime? TargetDate = null,
    string? Description = null,
    string? Icon = null,
    string? Color = null,
    AccountId? LinkedAccountId = null,
    GoalStatus? Status = null
);

public record GoalProgressDto(
    Money Amount,
    string? Notes = null
);