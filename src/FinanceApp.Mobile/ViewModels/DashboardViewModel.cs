namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly IDashboardService _dashboardService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly TransactionSheetRequest _sheetRequest;
    private readonly TransactionSheetService _transactionSheetService;
    private readonly DevDataSeeder _devDataSeeder;
    private readonly INotificationCenter _notificationCenter;
    private readonly NotificationWatcher _notificationWatcher;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    private int _unreadNotifications;

    [ObservableProperty]
    private DashboardDto? _dashboard;

    [ObservableProperty]
    private Money _totalBalance = Money.Zero();

    [ObservableProperty]
    private Money _totalIncome = Money.Zero();

    [ObservableProperty]
    private Money _totalExpense = Money.Zero();

    [ObservableProperty]
    private Money _netAmount = Money.Zero();

    [ObservableProperty]
    private decimal _savingsRate;

    [ObservableProperty]
    private IReadOnlyList<TransactionDto> _recentTransactions = Array.Empty<TransactionDto>();

    [ObservableProperty]
    private IReadOnlyList<CategorySpendingDto> _spendingByCategory = Array.Empty<CategorySpendingDto>();

    [ObservableProperty]
    private IReadOnlyList<BudgetDto> _activeBudgets = Array.Empty<BudgetDto>();

    [ObservableProperty]
    private IReadOnlyList<FinancialGoalDto> _activeGoals = Array.Empty<FinancialGoalDto>();

    [ObservableProperty]
    private string _userName = UserDisplay.FallbackName;

    [ObservableProperty]
    private string _userInitial = UserDisplay.FallbackInitial;

    public DashboardViewModel(
        IDashboardService dashboardService,
        IAuthenticationService authService,
        INavigationService navigationService,
        TransactionSheetRequest sheetRequest,
        TransactionSheetService transactionSheetService,
        DevDataSeeder devDataSeeder,
        INotificationCenter notificationCenter,
        NotificationWatcher notificationWatcher,
        IServiceScopeFactory scopeFactory,
        ILogger<DashboardViewModel> logger)
        : base(scopeFactory)
    {
        _dashboardService = dashboardService;
        _authService = authService;
        _navigationService = navigationService;
        _sheetRequest = sheetRequest;
        _transactionSheetService = transactionSheetService;
        _devDataSeeder = devDataSeeder;
        _notificationCenter = notificationCenter;
        _notificationWatcher = notificationWatcher;
        _scopeFactory = scopeFactory;
        _logger = logger;
        Title = "Dashboard";

        UnreadNotifications = _notificationCenter.UnreadCount;
    }

    /// <summary>
    /// Subscribes to badge changes. The centre is a singleton and this view model
    /// is not, so subscribing in the constructor leaks a handler per instance.
    /// </summary>
    public void Attach() => _notificationCenter.Changed += OnNotificationsChanged;

    public void Detach() => _notificationCenter.Changed -= OnNotificationsChanged;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        // A tab tap back to the dashboard should be instant. Shell keeps this page
        // alive, so the data is still here unless something wrote since.
        if (CanSkipReload()) return;

        IsBusy = true;
        ClearError();

        try
        {
            var email = await _authService.GetCurrentUserEmailAsync();
            UserName = UserDisplay.NameFromEmail(email);
            UserInitial = UserDisplay.InitialFromEmail(email);

            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                SetError("User not authenticated");
                return;
            }

            var dashboard = await QueryOffUiThreadAsync(async services =>
            {
                // Demo data seeding is disabled. It ran on every dashboard load
                // and wrote a few hundred rows into a brand new account, so a
                // real user saw figures they never entered - and, being writes,
                // they also queued sync operations for data the user did not
                // create.
                //
                // Still reachable on purpose from Forecasts and Budget Ideas via
                // their explicit "load sample data" action, which is a user
                // decision rather than something the app does behind their back.
                //
                // var seeder = services.GetRequiredService<DevDataSeeder>();
                // await seeder.SeedIfEmptyAsync(userId.Value);

                var dashboardService = services.GetRequiredService<IDashboardService>();
                return await dashboardService.GetDashboardAsync(userId.Value);
            });

            Dashboard = dashboard;

            TotalBalance = dashboard.TotalBalance;
            TotalIncome = dashboard.TotalIncome;
            TotalExpense = dashboard.TotalExpense;
            NetAmount = dashboard.NetAmount;
            SavingsRate = dashboard.SavingsRate;
            RecentTransactions = dashboard.RecentTransactions.Take(4).ToList();
            SpendingByCategory = dashboard.SpendingByCategory;
            ActiveBudgets = dashboard.ActiveBudgets;
            ActiveGoals = dashboard.ActiveGoals;

            MarkLoaded();

            // Keep the bell badge and any new alerts current on the landing page.
            await _notificationWatcher.RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard");
            SetError("Failed to load dashboard");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnNotificationsChanged()
    {
        // Raised by the watcher, which can run off the UI thread.
        if (MainThread.IsMainThread)
            UnreadNotifications = _notificationCenter.UnreadCount;
        else
            MainThread.BeginInvokeOnMainThread(() => UnreadNotifications = _notificationCenter.UnreadCount);
    }

    [RelayCommand]
    private async Task NavigateToTransactionsAsync()
    {
        await _navigationService.NavigateToAsync("//Transactions");
    }

    [RelayCommand]
    private Task NavigateToAddExpenseAsync() => OpenSheetAsync(TransactionType.Expense);

    [RelayCommand]
    private Task NavigateToAddIncomeAsync() => OpenSheetAsync(TransactionType.Income);

    /// <summary>
    /// Shows the form as an overlay on this page instead of pushing the
    /// AddTransactionSheet route, so the dashboard stays visible behind it.
    /// </summary>
    private Task OpenSheetAsync(TransactionType type)
    {
        _transactionSheetService.Show(type);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task NavigateToAccountsAsync()
    {
        await _navigationService.NavigateToAsync("//Accounts");
    }

    [RelayCommand]
    private async Task NavigateToBudgetsAsync()
    {
        await _navigationService.NavigateToAsync("//Budgets");
    }

    [RelayCommand]
    private async Task NavigateToGoalsAsync()
    {
        await _navigationService.NavigateToAsync("//Goals");
    }

    [RelayCommand]
    private async Task NavigateToAnalyticsAsync()
    {
        await _navigationService.NavigateToAsync("Analytics");
    }

    [RelayCommand]
    private async Task NavigateToMoreAsync()
    {
        await _navigationService.NavigateToAsync("//Main/More");
    }

    [RelayCommand]
    private async Task NavigateToAiSuggestionsAsync()
    {
        await _navigationService.NavigateToAsync("BudgetSuggestions");
    }

    [RelayCommand]
    private async Task NavigateToForecastsAsync()
    {
        await _navigationService.NavigateToAsync("Predictions");
    }

    [RelayCommand]
    private async Task NavigateToSettingsAsync()
    {
        await _navigationService.NavigateToAsync("//Settings");
    }

    [RelayCommand]
    private async Task NavigateToNotificationsAsync()
    {
        // Relative, not "//Notifications": Shell cannot absolute-navigate to a
        // route registered with Routing.RegisterRoute.
        await _navigationService.NavigateToAsync("Notifications");
    }

    [RelayCommand]
    private Task RefreshAsync() => RunRefreshAsync(LoadAsync);
}