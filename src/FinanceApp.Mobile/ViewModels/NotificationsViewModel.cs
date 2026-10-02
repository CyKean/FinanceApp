namespace FinanceApp.Mobile.ViewModels;

using System.Collections.ObjectModel;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Notifications;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class NotificationsViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private readonly INotificationCenter _center;
    private readonly NotificationWatcher _watcher;
    private readonly ILogger<NotificationsViewModel> _logger;

    [ObservableProperty]
    private int _unreadCount;

    [ObservableProperty]
    private bool _isEmpty = true;

    public ObservableCollection<AppNotification> Today { get; } = new();

    public ObservableCollection<AppNotification> Earlier { get; } = new();

    public NotificationsViewModel(
        INavigationService navigationService,
        INotificationCenter center,
        NotificationWatcher watcher,
        ILogger<NotificationsViewModel> logger)
    {
        _navigationService = navigationService;
        _center = center;
        _watcher = watcher;
        _logger = logger;

        Title = "Notifications";
    }

    /// <summary>Subscribes to feed changes. Called by the page on appearing.</summary>
    public void Attach() => _center.Changed += OnCenterChanged;

    /// <summary>Unsubscribes so the transient view-model does not outlive its page.</summary>
    public void Detach() => _center.Changed -= OnCenterChanged;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            await _watcher.RefreshAsync();
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
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private void MarkAllRead()
    {
        _center.MarkAllRead();
        Project();
    }

    [RelayCommand]
    private void Dismiss(AppNotification? item)
    {
        if (item is null) return;
        _center.Dismiss(item.Id);
        Project();
    }

    [RelayCommand]
    private void ClearAll()
    {
        _center.ClearAll();
        Project();
    }

    [RelayCommand]
    private async Task OpenAsync(AppNotification? item)
    {
        if (item is null) return;

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

    private void OnCenterChanged() => Project();

    private void Project()
    {
        var all = _center.Items.OrderByDescending(i => i.CreatedAt).ToList();
        var today = DateTime.Today;

        Today.Clear();
        Earlier.Clear();

        foreach (var item in all)
        {
            if (item.IsFromToday(today)) Today.Add(item);
            else Earlier.Add(item);
        }

        UnreadCount = _center.UnreadCount;
        IsEmpty = all.Count == 0;
        OnPropertyChanged(nameof(HasGroups));
    }

    public bool HasGroups => Today.Count > 0 || Earlier.Count > 0;
}