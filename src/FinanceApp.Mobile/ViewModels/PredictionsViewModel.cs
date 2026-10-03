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

public partial class PredictionsViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuthenticationService _authService;
    private readonly DevDataSeeder _devDataSeeder;
    private readonly ILogger<PredictionsViewModel> _logger;

    private CancellationTokenSource? _pending;

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

    [ObservableProperty]
    private bool _isPreparingSampleData;

    /// <summary>
    /// Drives the empty state. The prediction service always returns a DTO, even
    /// when it has too little data to predict from, so binding the empty state to
    /// "is the result null" never fired and the page showed a hero card full of
    /// zeros instead of explaining itself.
    /// </summary>
    public bool HasNoData =>
        ExpensePrediction is null ||
        (ExpensePrediction.Confidence == PredictionConfidence.InsufficientData &&
         ExpensePrediction.CategoryPredictions.Count == 0 &&
         SpendingTrends.Count == 0 &&
         BudgetForecasts.Count == 0);

    /// <summary>True once at least one section has content to render.</summary>
    public bool HasAnyData => !HasNoData;

    /// <summary>
    /// Per-section flags. The page keeps every section header visible once there
    /// is any data at all and swaps an empty hint in where a section has nothing,
    /// so a partly-populated page explains itself instead of silently dropping
    /// half its cards.
    /// </summary>
    public bool HasCategories => ExpensePrediction?.CategoryPredictions.Count > 0;

    public bool HasTrends => SpendingTrends.Count > 0;

    public bool HasBudgetForecasts => BudgetForecasts.Count > 0;

    public bool HasInsights => Insights.Count > 0;

    public bool HasAnomalies => Anomalies.Count > 0;

    private void RaiseSectionState()
    {
        OnPropertyChanged(nameof(HasNoData));
        OnPropertyChanged(nameof(HasAnyData));
        OnPropertyChanged(nameof(HasCategories));
        OnPropertyChanged(nameof(HasTrends));
        OnPropertyChanged(nameof(HasBudgetForecasts));
        OnPropertyChanged(nameof(HasInsights));
        OnPropertyChanged(nameof(HasAnomalies));
    }

    /// <summary>True when the account is empty, so sample data can be offered.</summary>
    public bool CanLoadSampleData { get; private set; }

    /// <summary>
    /// Assigned after an await, so it has to raise change notification itself -
    /// without this the retry button never appears for an account whose seeding
    /// attempt failed.
    /// </summary>
    private void SetCanLoadSampleData(bool value)
    {
        if (CanLoadSampleData == value) return;

        CanLoadSampleData = value;
        OnPropertyChanged(nameof(CanLoadSampleData));
    }

    public PredictionsViewModel(
        IServiceScopeFactory scopeFactory,
        IAuthenticationService authService,
        DevDataSeeder devDataSeeder,
        ILogger<PredictionsViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _authService = authService;
        _devDataSeeder = devDataSeeder;
        _logger = logger;
        Title = "Predictions";
    }

    partial void OnExpensePredictionChanged(ExpensePredictionDto? value) => RaiseSectionState();

    partial void OnSpendingTrendsChanged(IReadOnlyList<SpendingTrendDto> value) => RaiseSectionState();

    partial void OnBudgetForecastsChanged(IReadOnlyList<BudgetForecastDto> value) => RaiseSectionState();

    partial void OnInsightsChanged(IReadOnlyList<SmartInsightDto> value) => RaiseSectionState();

    partial void OnAnomaliesChanged(IReadOnlyList<AnomalyDetectionDto> value) => RaiseSectionState();

    /// <summary>Cancels in-flight work when the user leaves the page.</summary>
    public void Cancel()
    {
        var pending = _pending;
        _pending = null;

        try
        {
            pending?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already finished.
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        Cancel();
        var cts = new CancellationTokenSource();
        _pending = cts;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync(cts.Token);
            if (!userId.HasValue) return;

            // There is no async SQLite provider, so every repository call in this
            // pipeline completes synchronously on the calling thread. Running it
            // inline blocked the UI thread long enough for Android to raise
            // "Paytin isn't responding", so the whole block goes to a worker.
            //
            // It also resolves its own DI scope. MAUI resolves pages from the
            // root provider, and AddDbContext is registered Scoped, so every
            // root-resolved service shares one DbContext instance - sharing that
            // across a background thread while anything else touches it is what
            // made this page fail outright.
            //
            // Seeding deliberately does NOT happen here. It is a few hundred
            // inserts, and firing that off as the page opens held SQLite's write
            // lock while the page the user came from reloaded on the UI thread,
            // which is what produced "Paytin isn't responding" on back. The
            // dashboard already seeds an empty account at startup, and the button
            // below covers the case where that did not happen.
            var hasData = await _devDataSeeder.HasAnyDataAsync(userId.Value, cts.Token);

            var result = await Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateAsyncScope();
                var predictionService = scope.ServiceProvider.GetRequiredService<IPredictionService>();
                return await predictionService.GeneratePredictionAsync(userId.Value, cts.Token);
            }, cts.Token);

            if (cts.IsCancellationRequested) return;

            SetCanLoadSampleData(!hasData);

            PredictionResult = result;
            ExpensePrediction = result.ExpensePrediction;
            SpendingTrends = result.SpendingTrends;
            BudgetForecasts = result.BudgetForecasts;
            Insights = result.Insights;
            Anomalies = result.Anomalies;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Predictions load cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading predictions");
            SetError($"Couldn't load forecasts: {ex.Message}");
        }
        finally
        {
            cts.Dispose();
            if (ReferenceEquals(_pending, cts))
                _pending = null;

            IsPreparingSampleData = false;
            IsBusy = false;
        }
    }

    /// <summary>
    /// Writes sample data into a completely empty account so the page can be
    /// exercised. Never runs for an account that already has real transactions.
    /// </summary>
    [RelayCommand]
    private async Task LoadSampleDataAsync()
    {
        if (IsPreparingSampleData) return;

        IsPreparingSampleData = true;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            await Task.Run(() => _devDataSeeder.SeedIfEmptyAsync(userId.Value), CancellationToken.None);

            SetCanLoadSampleData(false);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sample data seeding failed");
        }
        finally
        {
            IsPreparingSampleData = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }
}