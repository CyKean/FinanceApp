namespace FinanceApp.Application.Mappings;

using FinanceApp.Application.DTOs;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public static class MappingExtensions
{
    public static AccountDto ToDto(this Account entity)
    {
        return new AccountDto(
            entity.Id,
            entity.Name,
            entity.Type,
            entity.Balance,
            entity.Description,
            entity.Icon,
            entity.Color,
            entity.IsDefault,
            entity.SortOrder,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.IsDeleted
        );
    }

    public static CategoryDto ToDto(this Category entity)
    {
        return new CategoryDto(
            entity.Id,
            entity.Name,
            entity.Type,
            entity.Icon,
            entity.Color,
            entity.ParentCategoryId,
            entity.IsSystem,
            entity.SortOrder,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.IsDeleted
        );
    }

    public static TransactionDto ToDto(this Transaction entity, string accountName, string categoryName, string categoryIcon, string categoryColor)
    {
        return new TransactionDto(
            entity.Id,
            entity.Type,
            entity.Amount,
            entity.Date,
            entity.Notes,
            entity.AccountId,
            accountName,
            entity.CategoryId,
            categoryName,
            categoryIcon,
            categoryColor,
            entity.RecurringTransactionId,
            entity.SyncStatus,
            entity.LastSyncedAt,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.IsDeleted
        );
    }

    public static BudgetDto ToDto(this Budget entity, string categoryName, string categoryIcon, string categoryColor)
    {
        return new BudgetDto(
            entity.Id,
            entity.Name,
            entity.Amount,
            entity.SpentAmount,
            entity.GetRemainingAmount(),
            entity.GetPercentageUsed(),
            entity.StartDate,
            entity.EndDate,
              entity.CategoryId,
              categoryName,
              categoryIcon,
              categoryColor,
              entity.Icon ?? categoryIcon,
              entity.Color ?? categoryColor,
              entity.IsOverBudget(),
            entity.IsNearLimit(),
            entity.SyncStatus,
            entity.LastSyncedAt,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.IsDeleted
        );
    }

    public static RecurringTransactionDto ToDto(this RecurringTransaction entity, string accountName, string categoryName)
    {
        return new RecurringTransactionDto(
            entity.Id,
            entity.Name,
            entity.Type,
            entity.Amount,
            entity.Frequency,
            entity.StartDate,
            entity.EndDate,
            entity.AccountId,
            accountName,
            entity.CategoryId,
            categoryName,
            entity.LastGeneratedAt,
            entity.NextDueDate,
            entity.IsActive,
            entity.Notes,
            entity.SyncStatus,
            entity.LastSyncedAt,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.IsDeleted
        );
    }

    public static FinancialGoalDto ToDto(this FinancialGoal entity, string? linkedAccountName = null)
    {
        return new FinancialGoalDto(
            entity.Id,
            entity.Name,
            entity.TargetAmount,
            entity.CurrentAmount,
            entity.GetRemainingAmount(),
            entity.GetProgressPercentage(),
            entity.TargetDate,
            entity.StartDate,
            entity.Status,
            entity.Description,
            entity.Icon,
            entity.Color,
            entity.LinkedAccountId,
            linkedAccountName,
            entity.GetDaysRemaining(),
            entity.GetRequiredMonthlySavings(),
            entity.IsOnTrack(entity.GetRequiredMonthlySavings()),
            entity.SyncStatus,
            entity.LastSyncedAt,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.IsDeleted
        );
    }

    public static SyncOperationDto ToDto(this SyncOperation entity)
    {
        return new SyncOperationDto(
            entity.Id,
            entity.EntityType,
            entity.EntityId,
            entity.OperationType,
            entity.Status,
            entity.RetryCount,
            entity.LastAttemptAt,
            entity.ErrorMessage,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }
}