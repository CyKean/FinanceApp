namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class PredictionsViewModel : BaseViewModel
{
    private readonly IPredictionService _predictionService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<PredictionsViewModel> _logger;

    [ObservableProperty]
    private PredictionResultDto? _predictionResult;

    [ObservableProperty]
    private ExpensePredictionDto? _expensePrediction;

    [ObservableProperty]
    private IReadOnlyList<SpendingTrendDto> _spendingTrends = Array.Empty<SpendingTrendDto>();

    [ObservableProperty]
    private IReadOnlyList<BudgetForecastDto> _budgetForecasts = Array.Empty<BudgetForecastDto>();

    [ObservableProperty]
    private IReadOnlyList<SmartInsightDto> _insights = Array.Empty<SmartInsightDto>();

    [ObservableProperty]
    private IReadOnlyList<AnomalyDetectionDto> _anomalies = Array.Empty<AnomalyDetectionDto>();

    public PredictionsViewModel(
        IPredictionService predictionService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<PredictionsViewModel> logger)
    {
        _predictionService = predictionService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
        Title = "Predictions";
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

            PredictionResult = await _predictionService.GeneratePredictionAsync(userId.Value);

            ExpensePrediction = PredictionResult.ExpensePrediction;
            SpendingTrends = PredictionResult.SpendingTrends;
            BudgetForecasts = PredictionResult.BudgetForecasts;
            Insights = PredictionResult.Insights;
            Anomalies = PredictionResult.Anomalies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading predictions");
            SetError("Failed to load predictions");
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
}
