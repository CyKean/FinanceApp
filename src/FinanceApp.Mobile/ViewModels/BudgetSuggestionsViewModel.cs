namespace FinanceApp.Mobile.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public partial class BudgetSuggestionsViewModel : BaseViewModel
{
    private readonly IBudgetService _budgetService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuthenticationService _authService;
    private readonly IDialogService _dialogService;
    private readonly DevDataSeeder _devDataSeeder;
    private readonly ILogger<BudgetSuggestionsViewModel> _logger;

    /// <summary>Suggestions the user has closed, so reloads do not resurrect them.</summary>
    private readonly HashSet<string> _dismissed = new(StringComparer.Ordinal);

    private CancellationTokenSource? _pending;

    [ObservableProperty]
    private IReadOnlyList<BudgetSuggestionDto> _suggestions = Array.Empty<BudgetSuggestionDto>();

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private bool _isPreparingSampleData;

    /// <summary>
    /// Drives the empty state. A BindableLayout has no EmptyView, so visibility
    /// is bound to this instead.
    /// </summary>
    public bool HasSuggestions => Suggestions.Count > 0;

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

    partial void OnSuggestionsChanged(IReadOnlyList<BudgetSuggestionDto> value) => OnPropertyChanged(nameof(HasSuggestions));

    public BudgetSuggestionsViewModel(
        IBudgetService budgetService,
        IServiceScopeFactory scopeFactory,
        IAuthenticationService authService,
        IDialogService dialogService,
        DevDataSeeder devDataSeeder,
        ILogger<BudgetSuggestionsViewModel> logger)
    {
        _budgetService = budgetService;
        _scopeFactory = scopeFactory;
        _authService = authService;
        _dialogService = dialogService;
        _devDataSeeder = devDataSeeder;
        _logger = logger;
        Title = "AI Suggestions";
    }

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

            // No async SQLite provider exists, so every call below completes
            // synchronously. Inlining them froze the UI thread and Android killed
            // the app. It also resolves its own scope: MAUI resolves pages from
            // the root provider and AddDbContext is Scoped, so root-resolved
            // services share a single DbContext that must not be used from a
            // background thread.
            //
            // Seeding deliberately does NOT happen here. It is a few hundred
            // inserts, and firing that off as the page opens held SQLite's write
            // lock while the page the user came from reloaded on the UI thread,
            // which is what produced "Finora isn't responding" on back. The
            // dashboard already seeds an empty account at startup, and the button
            // below covers the case where that did not happen.
            var hasData = await _devDataSeeder.HasAnyDataAsync(userId.Value, cts.Token);

            var loaded = await Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateAsyncScope();
                var suggestionService = scope.ServiceProvider.GetRequiredService<IBudgetSuggestionService>();
                return await suggestionService.GetSuggestionsAsync(userId.Value, cts.Token);
            }, cts.Token);

            if (cts.IsCancellationRequested) return;

            SetCanLoadSampleData(!hasData);
            Suggestions = loaded.Where(s => !_dismissed.Contains(s.Id)).ToList();

            if (Suggestions.Count == 0)
                SetError("No ideas yet - suggestions appear once a few weeks of spending are recorded.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Budget suggestions load cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading budget suggestions");
            SetError($"Couldn't load suggestions: {ex.Message}");
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

            // Demo data seeding is disabled, so this button no longer has anything
            // to do. The whole action is commented out along with the seeder body;
            // see DevDataSeeder.
            // await Task.Run(() => _devDataSeeder.SeedIfEmptyAsync(userId.Value), CancellationToken.None);

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
    private async Task ApplyAsync(BudgetSuggestionDto suggestion)
    {
        if (suggestion is null || IsApplying) return;

        IsApplying = true;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            if (suggestion.Kind == BudgetSuggestionKind.Create)
            {
                var today = DateTime.Today;
                var dto = new CreateBudgetDto(
                    $"{suggestion.CategoryName} budget",
                    suggestion.SuggestedAmount,
                    new DateTime(today.Year, today.Month, 1),
                    new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)),
                    suggestion.CategoryId,
                    string.IsNullOrEmpty(suggestion.CategoryIcon) ? null : suggestion.CategoryIcon,
                    string.IsNullOrEmpty(suggestion.CategoryColor) ? null : suggestion.CategoryColor);

                await _budgetService.CreateAsync(dto, userId.Value);
                await _dialogService.ShowSuccessAsync("Budget created");
            }
            else
            {
                if (!suggestion.TargetBudgetId.HasValue)
                {
                    await _dialogService.ShowFailureAsync("This suggestion no longer matches a budget");
                    return;
                }

                var dto = new UpdateBudgetDto(Amount: suggestion.SuggestedAmount);
                await _budgetService.UpdateAsync(suggestion.TargetBudgetId.Value, dto, userId.Value);
                await _dialogService.ShowSuccessAsync("Budget updated");
            }

            // Applied for good: remember it so the next reload does not offer it again.
            _dismissed.Add(suggestion.Id);
            Suggestions = Suggestions.Where(s => s.Id != suggestion.Id).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying budget suggestion {SuggestionId}", suggestion.Id);
            await _dialogService.ShowFailureAsync(DescribeFailure(ex));
        }
        finally
        {
            IsApplying = false;
        }
    }

    [RelayCommand]
    private void Dismiss(BudgetSuggestionDto suggestion)
    {
        if (suggestion is null) return;
        _dismissed.Add(suggestion.Id);
        Suggestions = Suggestions.Where(s => s.Id != suggestion.Id).ToList();
    }

    [RelayCommand]
    private Task RefreshAsync() => RunRefreshAsync(LoadAsync);

    /// <summary>
    /// Validation failures come from FluentValidation, not the domain, so
    /// catching only the domain type hid the real reason behind a generic
    /// "couldn't apply" message.
    /// </summary>
    private static string DescribeFailure(Exception ex) => ex switch
    {
        FinanceApp.Domain.Exceptions.ValidationException domain => domain.Message,
        FluentValidation.ValidationException fluent => string.Join(" ", fluent.Errors.Select(e => e.ErrorMessage)),
        _ => "Couldn't apply the suggestion"
    };
}