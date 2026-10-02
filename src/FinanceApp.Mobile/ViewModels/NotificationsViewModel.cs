namespace FinanceApp.Mobile.ViewModels;

using System.Collections.ObjectModel;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class NotificationsViewModel : BaseViewModel
{
    private readonly IBudgetService _budgetService;
    private readonly IFinancialGoalService _goalService;
    private readonly IRecurringTransactionService _recurringService;
    private readonly IPredictionService _predictionService;
    private readonly ISyncService _syncService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly NotificationCenter _center;
    private readonly ILogger<NotificationsViewModel> _logger;

    [ObservableProperty]
    private int _unreadCount;

    [ObservableProperty]
    private bool _isEmpty = true;

    public ObservableCollection<NotificationItem> Today { get; } = new();

    public ObservableCollection<NotificationItem> Earlier { get; } = new();

    public NotificationsViewModel(
        IBudgetService budgetService,
        IFinancialGoalService goalService,
        IRecurringTransactionService recurringService,
        IPredictionService predictionService,
        ISyncService syncService,
        IAuthenticationService authService,
        INavigationService navigationService,
        NotificationCenter center,
        ILogger<NotificationsViewModel> logger)
    {
        _budgetService = budgetService;
        _goalService = goalService;
        _recurringService = recurringService;
        _predictionService = predictionService;
        _syncService = syncService;
        _authService = authService;
        _navigationService = navigationService;
        _center = center;
        _logger = logger;

        Title = "Notifications";
        _center.Changed += OnCenterChanged;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            var items = new List<NotificationItem>();
            items.AddRange(await BuildBudgetItemsAsync(userId.Value));
            items.AddRange(await BuildGoalItemsAsync(userId.Value));
            items.AddRange(await BuildRecurringItemsAsync(userId.Value));
            items.AddRange(await BuildInsightItemsAsync(userId.Value));
            items.AddRange(await BuildSyncItemsAsync(userId.Value));

            _center.Publish(items);
            Project();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building notification feed");
            SetError("Failed to load notifications");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void MarkAllRead()
    {
        _center.MarkAllRead();
        Project();
    }

    [RelayCommand]
    private void Dismiss(NotificationItem? item)
    {
        if (item == null) return;
        _center.Dismiss(item.Id);
        Project();
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        _center.ClearAll();
        Project();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenAsync(NotificationItem? item)
    {
        if (item == null) return;

        _center.MarkRead(item.Id);
        Project();

        if (string.IsNullOrWhiteSpace(item.Destination)) return;

        try
        {
            await _navigationService.NavigateToAsync(item.Destination!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not navigate from notification {Id}", item.Id);
        }
    }

    private async Task<List<NotificationItem>> BuildBudgetItemsAsync(Guid userId)
    {
        var list = new List<NotificationItem>();
        var budgets = await _budgetService.GetActiveAsync(userId, DateTime.UtcNow);
        var now = DateTime.UtcNow;

        foreach (var budget in budgets)
        {
            if (budget.IsOverBudget)
            {
                list.Add(new NotificationItem(
                    $"budget-over-{budget.Id}",
                    "Budgets",
                    $"{budget.Name} is over budget",
                    $"You have spent {budget.PercentageUsed:F0}% of {budget.Amount.Amount:N0}. Time to cut back or adjust the limit.",
                    "alertCircle",
                    NotificationSeverity.Critical,
                    now,
                    "//Budgets"));
            }
            else if (budget.IsNearLimit)
            {
                list.Add(new NotificationItem(
                    $"budget-near-{budget.Id}",
                    "Budgets",
                    $"{budget.Name} is close to its limit",
                    $"{budget.PercentageUsed:F0}% used · {budget.RemainingAmount.Amount:N0} left of {budget.Amount.Amount:N0}.",
                    "pie",
                    NotificationSeverity.Warning,
                    now,
                    "//Budgets"));
            }
        }

        return list;
    }

    private async Task<List<NotificationItem>> BuildGoalItemsAsync(Guid userId)
    {
        var list = new List<NotificationItem>();
        var goals = await _goalService.GetActiveAsync(userId);
        var now = DateTime.UtcNow;

        foreach (var goal in goals)
        {
            if (goal.DaysRemaining <= 7)
            {
                list.Add(new NotificationItem(
                    $"goal-due-{goal.Id}",
                    "Goals",
                    $"{goal.Name} is due soon",
                    goal.DaysRemaining == 0
                        ? "Target date is today."
                        : $"{goal.DaysRemaining} day(s) left · {goal.ProgressPercentage:F0}% saved.",
                    "target",
                    goal.DaysRemaining <= 3 ? NotificationSeverity.Warning : NotificationSeverity.Info,
                    now,
                    "//Goals"));
            }

            if (goal.ProgressPercentage >= 100)
            {
                list.Add(new NotificationItem(
                    $"goal-done-{goal.Id}",
                    "Goals",
                    $"{goal.Name} reached its target",
                    "Nicely done. Mark it complete or set a bigger goal.",
                    "star",
                    NotificationSeverity.Success,
                    now,
                    "//Goals"));
            }
        }

        return list;
    }

    private async Task<List<NotificationItem>> BuildRecurringItemsAsync(Guid userId)
    {
        var list = new List<NotificationItem>();
        var recurring = await _recurringService.GetActiveAsync(userId);
        var today = DateTime.UtcNow.Date;

        foreach (var item in recurring)
        {
            var due = item.NextDueDate;
            if (due == null && item.StartDate.Date >= today)
                due = item.StartDate;

            if (due == null) continue;

            var days = (due.Value.Date - today).Days;
            if (days is < 0 or > 7) continue;

            list.Add(new NotificationItem(
                $"recurring-{item.Id}-{due.Value:yyyyMMdd}",
                "Bills",
                $"{item.Name} {(days == 0 ? "is due today" : days == 1 ? "is due tomorrow" : $"is due in {days} days")}",
                $"{item.Amount.Amount:N0} · {item.Frequency}",
                days <= 1 ? "alert" : "receipt",
                days <= 1 ? NotificationSeverity.Warning : NotificationSeverity.Info,
                today,
                "//Main/Transactions"));
        }

        return list;
    }

    private async Task<List<NotificationItem>> BuildInsightItemsAsync(Guid userId)
    {
        var list = new List<NotificationItem>();

        try
        {
            var result = await _predictionService.GeneratePredictionAsync(userId);

            foreach (var anomaly in result.Anomalies)
            {
                list.Add(new NotificationItem(
                    $"anomaly-{anomaly.TransactionId}",
                    "Insights",
                    $"Unusual {anomaly.CategoryName} spending",
                    $"{anomaly.Message} ({anomaly.DeviationPercentage:+0;-0}%)",
                    "alertCircle",
                    NotificationSeverity.Critical,
                    anomaly.TransactionDate,
                    "//Main/Transactions"));
            }

            foreach (var insight in result.Insights.Where(i => i.Severity != Application.DTOs.InsightSeverity.Info))
            {
                list.Add(new NotificationItem(
                    $"insight-{insight.Type}-{insight.RelatedEntityId}",
                    "Insights",
                    insight.Title,
                    insight.Message,
                    "bulb",
                    insight.Severity == Application.DTOs.InsightSeverity.Critical
                        ? NotificationSeverity.Critical
                        : NotificationSeverity.Warning,
                    DateTime.UtcNow,
                    "//Main/More"));
            }
        }
        catch (Exception ex)
        {
            // Insights are a nice-to-have; never block the feed on them.
            _logger.LogWarning(ex, "Prediction insights unavailable for notifications");
        }

        return list;
    }

    private async Task<List<NotificationItem>> BuildSyncItemsAsync(Guid userId)
    {
        var list = new List<NotificationItem>();

        try
        {
            var status = await _syncService.GetStatusAsync(userId);

            if (status.FailedCount > 0)
            {
                list.Add(new NotificationItem(
                    "sync-failed",
                    "Sync",
                    $"{status.FailedCount} change(s) failed to sync",
                    status.LastError ?? "Open Settings to retry the sync.",
                    "alert",
                    NotificationSeverity.Critical,
                    status.LastSyncAt ?? DateTime.UtcNow,
                    "//Settings"));
            }
            else if (status.PendingCount > 0)
            {
                list.Add(new NotificationItem(
                    "sync-pending",
                    "Sync",
                    $"{status.PendingCount} change(s) waiting to sync",
                    "They will upload automatically on the next sync.",
                    "sync",
                    NotificationSeverity.Info,
                    status.LastSyncAt ?? DateTime.UtcNow,
                    "//Settings"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sync status unavailable for notifications");
        }

        return list;
    }

    private void OnCenterChanged() => Project();

    private void Project()
    {
        var all = _center.Items.OrderByDescending(i => i.CreatedAt).ToList();
        var cutoff = DateTime.UtcNow.Date;

        Today.Clear();
        Earlier.Clear();

        foreach (var item in all)
        {
            if (item.CreatedAt.Date >= cutoff) Today.Add(item);
            else Earlier.Add(item);
        }

        UnreadCount = _center.UnreadCount;
        IsEmpty = all.Count == 0;
        OnPropertyChanged(nameof(HasGroups));
    }

    public bool HasGroups => Today.Count > 0 || Earlier.Count > 0;
}