namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class AnalyticsViewModel : BaseViewModel
{
    private readonly IDashboardService _dashboardService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<AnalyticsViewModel> _logger;

    [ObservableProperty]
    private AnalyticsDto? _analytics;

    [ObservableProperty]
    private int _selectedMonths = 6;

    [ObservableProperty]
    private bool _showIncomeChart;

    [ObservableProperty]
    private bool _showExpenseChart;

    [ObservableProperty]
    private bool _showSavingsChart;

    public AnalyticsViewModel(
        IDashboardService dashboardService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<AnalyticsViewModel> logger)
    {
        _dashboardService = dashboardService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
        Title = "Analytics";
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

            Analytics = await _dashboardService.GetAnalyticsAsync(userId.Value, SelectedMonths, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading analytics");
            SetError("Failed to load analytics");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ChangePeriodAsync(int months)
    {
        SelectedMonths = months;
        await LoadAsync();
    }

    partial void OnSelectedMonthsChanged(int value)
    {
        _ = LoadAsync();
    }
}