namespace FinanceApp.Mobile.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Mobile.Services;
using Microsoft.Extensions.Logging;

public partial class BudgetSuggestionsViewModel : BaseViewModel
{
    private readonly IBudgetSuggestionService _suggestionService;
    private readonly IBudgetService _budgetService;
    private readonly IAuthenticationService _authService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<BudgetSuggestionsViewModel> _logger;

    /// <summary>Suggestions the user has closed, so reloads do not resurrect them.</summary>
    private readonly HashSet<string> _dismissed = new(StringComparer.Ordinal);

    [ObservableProperty]
    private IReadOnlyList<BudgetSuggestionDto> _suggestions = Array.Empty<BudgetSuggestionDto>();

    [ObservableProperty]
    private bool _isApplying;

    public BudgetSuggestionsViewModel(
        IBudgetSuggestionService suggestionService,
        IBudgetService budgetService,
        IAuthenticationService authService,
        IDialogService dialogService,
        ILogger<BudgetSuggestionsViewModel> logger)
    {
        _suggestionService = suggestionService;
        _budgetService = budgetService;
        _authService = authService;
        _dialogService = dialogService;
        _logger = logger;
        Title = "AI Suggestions";
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

            var loaded = await _suggestionService.GetSuggestionsAsync(userId.Value);
            Suggestions = loaded.Where(s => !_dismissed.Contains(s.Id)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading budget suggestions");
            SetError("Failed to load suggestions");
        }
        finally
        {
            IsBusy = false;
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
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    /// <summary>
    /// Validation failures come from FluentValidation, not the domain, so
    /// catching only the domain type hid the real reason behind a generic
    /// "couldn't apply" message.
    /// </summary>
    private static string DescribeFailure(Exception ex) => ex switch
    {
        FinanceApp.Domain.Exceptions.ValidationException domain =>
            domain.Message,
        FluentValidation.ValidationException fluent =>
            string.Join(" ", fluent.Errors.Select(e => e.ErrorMessage)),
        _ => "Couldn't apply the suggestion"
    };
}
