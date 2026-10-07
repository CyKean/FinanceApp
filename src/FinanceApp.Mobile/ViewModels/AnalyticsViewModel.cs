namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public partial class AnalyticsViewModel : BaseViewModel
{
    private readonly IDashboardService _dashboardService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<AnalyticsViewModel> _logger;

    [ObservableProperty]
    private StatisticsDto? _statistics;

    [ObservableProperty]
    private StatisticsPeriod _selectedPeriod = StatisticsPeriod.Month;

    [ObservableProperty]
    private string _earningChangeText = string.Empty;

    [ObservableProperty]
    private string _spendingChangeText = string.Empty;

    [ObservableProperty]
    private string _goalText = string.Empty;

    [ObservableProperty]
    private bool _hasGoal;

    public AnalyticsViewModel(
        IDashboardService dashboardService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        IServiceScopeFactory scopeFactory,
        ILogger<AnalyticsViewModel> logger)
        : base(scopeFactory)
    {
        _dashboardService = dashboardService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
        Title = "Statistics";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        if (CanSkipReload()) return;

        IsBusy = true;
        ClearError();

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            var period = SelectedPeriod;

            Statistics = await QueryOffUiThreadAsync(services =>
                services.GetRequiredService<IDashboardService>().GetStatisticsAsync(userId.Value, period, CancellationToken.None));

            UpdateChangeTexts();

            MarkLoaded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading statistics");
            SetError("Failed to load statistics");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        InvalidateLoad();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ChangePeriodAsync(string period)
    {
        if (!Enum.TryParse<StatisticsPeriod>(period, true, out var value)) return;
        SelectedPeriod = value;
        InvalidateLoad();
        await LoadAsync();
    }

    private void UpdateChangeTexts()
    {
        EarningChangeText = DescribeChange(Statistics?.EarningChangePercent, Statistics?.TotalEarning);
        SpendingChangeText = DescribeChange(Statistics?.SpendingChangePercent, Statistics?.TotalSpending);
        HasGoal = Statistics?.PrimaryGoal is not null;
        GoalText = Statistics?.PrimaryGoal is { } goal
            ? $"{goal.CurrentAmount}/{goal.TargetAmount}"
            : string.Empty;
    }

    private static string DescribeChange(decimal? percent, Money? current)
    {
        if (percent is null)
            return current is not null && current.Amount > 0 ? "New" : "No change";

        var direction = percent >= 0 ? "increased" : "decreased";
        return $"{direction} by {Math.Abs(percent.Value):0.#}%";
    }

    partial void OnSelectedPeriodChanged(StatisticsPeriod value)
    {
        // DataTriggers on the period pills bind to SelectedPeriod, so a fresh
        // load refreshes the highlight automatically.
    }
}
