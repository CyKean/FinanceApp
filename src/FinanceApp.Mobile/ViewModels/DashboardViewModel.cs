namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly IDashboardService _dashboardService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<DashboardViewModel> _logger;

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

    public DashboardViewModel(
        IDashboardService dashboardService,
        IAuthenticationService authService,
        INavigationService navigationService,
        ILogger<DashboardViewModel> logger)
    {
        _dashboardService = dashboardService;
        _authService = authService;
        _navigationService = navigationService;
        _logger = logger;
        Title = "Dashboard";
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
            if (!userId.HasValue)
            {
                SetError("User not authenticated");
                return;
            }

            Dashboard = await _dashboardService.GetDashboardAsync(userId.Value);

            TotalBalance = Dashboard.TotalBalance;
            TotalIncome = Dashboard.TotalIncome;
            TotalExpense = Dashboard.TotalExpense;
            NetAmount = Dashboard.NetAmount;
            SavingsRate = Dashboard.SavingsRate;
            RecentTransactions = Dashboard.RecentTransactions;
            SpendingByCategory = Dashboard.SpendingByCategory;
            ActiveBudgets = Dashboard.ActiveBudgets;
            ActiveGoals = Dashboard.ActiveGoals;
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

    [RelayCommand]
    private async Task NavigateToTransactionsAsync()
    {
        await _navigationService.NavigateToAsync("//Transactions");
    }

    [RelayCommand]
    private async Task NavigateToAddExpenseAsync()
    {
        await _navigationService.NavigateToAsync("//AddTransaction?type=Expense");
    }

    [RelayCommand]
    private async Task NavigateToAddIncomeAsync()
    {
        await _navigationService.NavigateToAsync("//AddTransaction?type=Income");
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
        await _navigationService.NavigateToAsync("//Analytics");
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }
}