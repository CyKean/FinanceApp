namespace FinanceApp.Application.Interfaces;

public interface INotificationService
{
    Task ScheduleBudgetWarningAsync(Guid budgetId, Guid userId, CancellationToken cancellationToken = default);
    Task ScheduleRecurringBillAsync(Guid recurringTransactionId, Guid userId, CancellationToken cancellationToken = default);
    Task ScheduleGoalProgressAsync(Guid goalId, Guid userId, CancellationToken cancellationToken = default);
    Task CancelScheduledNotificationsAsync(Guid entityId, Guid userId, CancellationToken cancellationToken = default);
    Task RequestPermissionAsync(CancellationToken cancellationToken = default);
    Task<bool> HasPermissionAsync(CancellationToken cancellationToken = default);
}