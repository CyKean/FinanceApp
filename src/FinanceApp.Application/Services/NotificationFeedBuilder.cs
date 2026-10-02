namespace FinanceApp.Application.Services;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Notifications;
using Microsoft.Extensions.Logging;

/// <summary>
/// Turns budgets, goals, recurring bills, prediction insights and sync status
/// into the in-app notification feed.
/// <para>
/// Dates are handled in local time on purpose: budgets, recurring bills and
/// transactions are all stored with the dates the user entered, so comparing
/// them against <see cref="DateTime.UtcNow"/> would shift "today" by the UTC
/// offset and silently hide every alert for part of the day.
/// </para>
/// <para>
/// Every source is isolated: one failing query degrades that section only and
/// never empties the whole feed.
/// </para>
/// </summary>
public class NotificationFeedBuilder : INotificationFeedBuilder
{
    /// <summary>Days before a recurring bill that it starts showing up.</summary>
    private const int BillHorizonDays = 7;

    /// <summary>Days before a goal target date that it starts warning.</summary>
    private const int GoalHorizonDays = 7;

    private readonly IBudgetService _budgetService;
    private readonly IFinancialGoalService _goalService;
    private readonly IRecurringTransactionService _recurringService;
    private readonly IPredictionService _predictionService;
    private readonly ISyncService _syncService;
    private readonly ILogger<NotificationFeedBuilder> _logger;

    public NotificationFeedBuilder(
        IBudgetService budgetService,
        IFinancialGoalService goalService,
        IRecurringTransactionService recurringService,
        IPredictionService predictionService,
        ISyncService syncService,
        ILogger<NotificationFeedBuilder> logger)
    {
        _budgetService = budgetService;
        _goalService = goalService;
        _recurringService = recurringService;
        _predictionService = predictionService;
        _syncService = syncService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AppNotification>> BuildAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var items = new List<AppNotification>();

        items.AddRange(await BuildBudgetItemsAsync(userId, now, cancellationToken));
        items.AddRange(await BuildGoalItemsAsync(userId, now, cancellationToken));
        items.AddRange(await BuildRecurringItemsAsync(userId, now, cancellationToken));
        items.AddRange(await BuildInsightItemsAsync(userId, now, cancellationToken));
        items.AddRange(await BuildSyncItemsAsync(userId, now, cancellationToken));

        return items
            .GroupBy(i => i.Id, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
    }

    private async Task<IReadOnlyList<AppNotification>> BuildBudgetItemsAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var items = new List<AppNotification>();

        try
        {
            var budgets = await _budgetService.GetActiveAsync(userId, now, cancellationToken);

            foreach (var budget in budgets)
            {
                var spent = budget.SpentAmount.Amount;
                var limit = budget.Amount.Amount;
                var used = (decimal)budget.PercentageUsed;

                if (budget.IsOverBudget)
                {
                    items.Add(new AppNotification(
                        $"budget-over-{budget.Id}",
                        "Budgets",
                        $"{budget.Name} is over budget",
                        $"Spent {budget.SpentAmount.Amount:N0} of {limit:N0} ({used:F0}%). Cut back or raise the limit.",
                        "alertCircle",
                        NotificationSeverity.Critical,
                        now,
                        "//Budgets"));
                }
                else if (budget.IsNearLimit)
                {
                    items.Add(new AppNotification(
                        $"budget-near-{budget.Id}",
                        "Budgets",
                        $"{budget.Name} is close to its limit",
                        $"{used:F0}% used · {Math.Max(limit - spent, 0):N0} left of {limit:N0}.",
                        "pie",
                        NotificationSeverity.Warning,
                        now,
                        "//Budgets"));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Budget notifications unavailable");
        }

        return items;
    }

    private async Task<IReadOnlyList<AppNotification>> BuildGoalItemsAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var items = new List<AppNotification>();

        try
        {
            var goals = await _goalService.GetActiveAsync(userId, cancellationToken);

            foreach (var goal in goals)
            {
                if (goal.ProgressPercentage >= 100)
                {
                    items.Add(new AppNotification(
                        $"goal-done-{goal.Id}",
                        "Goals",
                        $"{goal.Name} reached its target",
                        $"{goal.CurrentAmount.Amount:N0} of {goal.TargetAmount.Amount:N0} saved. Mark it complete or set a bigger goal.",
                        "star",
                        NotificationSeverity.Success,
                        now,
                        "//Goals"));
                }
                else if (goal.DaysRemaining <= GoalHorizonDays)
                {
                    var overdue = goal.DaysRemaining < 0;
                    var title = overdue
                        ? $"{goal.Name} is past its target date"
                        : goal.DaysRemaining == 0
                            ? $"{goal.Name} is due today"
                            : $"{goal.Name} is due in {goal.DaysRemaining} day(s)";

                    items.Add(new AppNotification(
                        $"goal-due-{goal.Id}",
                        "Goals",
                        title,
                        overdue
                            ? $"Still {goal.ProgressPercentage:F0}% saved · {goal.RemainingAmount.Amount:N0} to go."
                            : $"{goal.ProgressPercentage:F0}% saved · {goal.RemainingAmount.Amount:N0} to go.",
                        "target",
                        overdue || goal.DaysRemaining <= 3
                            ? NotificationSeverity.Warning
                            : NotificationSeverity.Info,
                        now,
                        "//Goals"));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Goal notifications unavailable");
        }

        return items;
    }

    private async Task<IReadOnlyList<AppNotification>> BuildRecurringItemsAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var items = new List<AppNotification>();

        try
        {
            var recurring = await _recurringService.GetActiveAsync(userId, cancellationToken);
            var today = now.Date;

            foreach (var bill in recurring)
            {
                var due = bill.NextDueDate?.Date
                    ?? (bill.StartDate.Date >= today ? bill.StartDate.Date : (DateTime?)null);

                if (due is null)
                    continue;

                var days = (due.Value - today).Days;
                if (days is < -7 or > BillHorizonDays)
                    continue;

                items.Add(new AppNotification(
                    $"recurring-{bill.Id}-{due.Value:yyyyMMdd}",
                    "Bills",
                    days switch
                    {
                        < 0 => $"{bill.Name} is {Math.Abs(days)} day(s) overdue",
                        0 => $"{bill.Name} is due today",
                        1 => $"{bill.Name} is due tomorrow",
                        _ => $"{bill.Name} is due in {days} days"
                    },
                    $"{bill.Amount.Amount:N0} · {bill.Frequency}",
                    days <= 1 ? "alert" : "receipt",
                    days switch
                    {
                        < 0 => NotificationSeverity.Critical,
                        <= 1 => NotificationSeverity.Warning,
                        _ => NotificationSeverity.Info
                    },
                    days < 0 ? due.Value.AddDays(1) : today,
                    "//Main/Transactions"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Recurring bill notifications unavailable");
        }

        return items;
    }

    private async Task<IReadOnlyList<AppNotification>> BuildInsightItemsAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var items = new List<AppNotification>();

        try
        {
            var result = await _predictionService.GeneratePredictionAsync(userId, cancellationToken);

            foreach (var anomaly in result.Anomalies)
            {
                items.Add(new AppNotification(
                    $"anomaly-{anomaly.TransactionId}",
                    "Insights",
                    $"Unusual {anomaly.CategoryName} spending",
                    $"{anomaly.Message} ({anomaly.DeviationPercentage:+0;-0}%)",
                    "alertCircle",
                    NotificationSeverity.Critical,
                    anomaly.TransactionDate,
                    "//Main/Transactions"));
            }

            foreach (var insight in result.Insights.Where(i => i.Severity != InsightSeverity.Info))
            {
                items.Add(new AppNotification(
                    $"insight-{insight.Type}-{insight.RelatedEntityId?.ToString() ?? "general"}",
                    "Insights",
                    insight.Title,
                    insight.Message,
                    "bulb",
                    insight.Severity == InsightSeverity.Critical
                        ? NotificationSeverity.Critical
                        : NotificationSeverity.Warning,
                    now,
                    "//Main/More"));
            }

            foreach (var forecast in result.BudgetForecasts.Where(f => f.WillExceedBudget))
            {
                items.Add(new AppNotification(
                    $"forecast-over-{forecast.BudgetId}",
                    "Insights",
                    $"{forecast.BudgetName} is on track to overshoot",
                    $"Forecast {forecast.PredictedSpent.Amount:N0} against a {forecast.BudgetAmount.Amount:N0} limit ({forecast.ExceedPercentage:+0;-0}%).",
                    "forecast",
                    NotificationSeverity.Warning,
                    now,
                    "//Budgets"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Prediction insights unavailable for notifications");
        }

        return items;
    }

    private async Task<IReadOnlyList<AppNotification>> BuildSyncItemsAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var items = new List<AppNotification>();

        try
        {
            var status = await _syncService.GetStatusAsync(userId, cancellationToken);
            var since = status.LastSyncAt ?? now;

            if (status.FailedCount > 0)
            {
                items.Add(new AppNotification(
                    "sync-failed",
                    "Sync",
                    $"{status.FailedCount} change(s) failed to sync",
                    string.IsNullOrWhiteSpace(status.LastError)
                        ? "Open Settings to retry the sync."
                        : status.LastError,
                    "alert",
                    NotificationSeverity.Critical,
                    since,
                    "//Settings"));
            }
            else if (status.PendingCount > 0)
            {
                items.Add(new AppNotification(
                    "sync-pending",
                    "Sync",
                    $"{status.PendingCount} change(s) waiting to sync",
                    "They will upload automatically on the next sync.",
                    "sync",
                    NotificationSeverity.Info,
                    since,
                    "//Settings"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sync status unavailable for notifications");
        }

        return items;
    }
}