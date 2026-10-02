namespace FinanceApp.Mobile.Views;

using FinanceApp.Application.Interfaces;
using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.Services;
using System.Diagnostics;

public partial class MorePage : ContentPage
{
    private readonly IAuthenticationService _authService;
    private readonly INotificationCenter _notificationCenter;

    public MorePage(IAuthenticationService authService, INotificationCenter notificationCenter)
    {
        InitializeComponent();
        _authService = authService;
        _notificationCenter = notificationCenter;

        UpdateNotificationBadge(_notificationCenter.UnreadCount);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // The centre is a singleton, so re-subscribe each time the tab is shown.
        _notificationCenter.Changed -= OnNotificationsChanged;
        _notificationCenter.Changed += OnNotificationsChanged;

        var email = await _authService.GetCurrentUserEmailAsync();
        ProfileName.Text = UserDisplay.NameFromEmail(email);
        AvatarInitial.Text = UserDisplay.InitialFromEmail(email);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _notificationCenter.Changed -= OnNotificationsChanged;
    }

    private async void OnNotificationsTapped(object? sender, EventArgs e)
    {
        try
        {
            // Relative route: Shell throws on absolute ("//Notifications")
            // navigation to a route registered via Routing.RegisterRoute.
            await Shell.Current.GoToAsync("Notifications");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Notifications] navigation failed: {ex.Message}");
            await Shell.Current.GoToAsync("//Main/Dashboard");
        }
    }

    private void OnNotificationsChanged() =>
        OnMainThread(() => UpdateNotificationBadge(_notificationCenter.UnreadCount));

    private void UpdateNotificationBadge(int unread)
    {
        NotificationBadge.IsVisible = unread > 0;
        NotificationBadgeCount.Text = unread > 99 ? "99+" : unread.ToString();
    }

    private static void OnMainThread(Action action)
    {
        if (MainThread.IsMainThread)
            action();
        else
            MainThread.BeginInvokeOnMainThread(action);
    }

    private async void OnGoalsTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Goals");
    }

    private async void OnCategoriesTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Categories");
    }

    private async void OnAnalyticsTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Analytics");
    }

    private async void OnChatTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Chat");
    }

    private async void OnBudgetSuggestionsTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("BudgetSuggestions");
    }

    private async void OnCalendarTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Calendar");
    }

    private async void OnPredictionsTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Predictions");
    }

    private async void OnProfileTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Settings");
    }

    private async void OnScrimTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Main/Dashboard");
    }

    private async void OnHomeTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Main/Dashboard");
    }

    private async void OnTransactionsTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Main/Transactions");
    }
}