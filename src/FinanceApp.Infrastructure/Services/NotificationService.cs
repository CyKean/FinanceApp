namespace FinanceApp.Infrastructure.Services;

using FinanceApp.Application.Interfaces;
using Microsoft.Extensions.Logging;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public Task ScheduleBudgetWarningAsync(Guid budgetId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scheduled budget warning for budget {BudgetId}, user {UserId}", budgetId, userId);
        return Task.CompletedTask;
    }

    public Task ScheduleRecurringBillAsync(Guid recurringTransactionId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scheduled recurring bill notification for recurring transaction {RecurringTransactionId}, user {UserId}", recurringTransactionId, userId);
        return Task.CompletedTask;
    }

    public Task ScheduleGoalProgressAsync(Guid goalId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scheduled goal progress notification for goal {GoalId}, user {UserId}", goalId, userId);
        return Task.CompletedTask;
    }

    public Task CancelScheduledNotificationsAsync(Guid entityId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cancelled scheduled notifications for entity {EntityId}, user {UserId}", entityId, userId);
        return Task.CompletedTask;
    }

    public Task RequestPermissionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Notification permission requested");
        return Task.CompletedTask;
    }

    public Task<bool> HasPermissionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}