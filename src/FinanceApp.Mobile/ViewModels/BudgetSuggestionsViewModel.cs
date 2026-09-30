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

    [ObservableProperty]
    private IReadOnlyList<BudgetSuggestionDto> _suggestions = Array.Empty<BudgetSuggestionDto>();

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

            Suggestions = await _suggestionService.GetSuggestionsAsync(userId.Value);
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
        if (suggestion is null) return;

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

            Suggestions = Suggestions.Where(s => s.Id != suggestion.Id).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying budget suggestion {SuggestionId}", suggestion.Id);
            if (ex is FinanceApp.Domain.Exceptions.ValidationException vex)
                await _dialogService.ShowFailureAsync(vex.Message);
            else
                await _dialogService.ShowFailureAsync("Couldn't apply the suggestion");
        }
    }

    [RelayCommand]
    private void Dismiss(BudgetSuggestionDto suggestion)
    {
        if (suggestion is null) return;
        Suggestions = Suggestions.Where(s => s.Id != suggestion.Id).ToList();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }
}
