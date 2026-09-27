namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class BudgetsViewModel : BaseViewModel
{
    private readonly IBudgetService _budgetService;
    private readonly ICategoryService _categoryService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<BudgetsViewModel> _logger;

    [ObservableProperty]
    private IReadOnlyList<BudgetDto> _budgets = Array.Empty<BudgetDto>();

    [ObservableProperty]
    private DateTime _currentMonth = DateTime.Today;

    public BudgetsViewModel(
        IBudgetService budgetService,
        ICategoryService categoryService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<BudgetsViewModel> logger)
    {
        _budgetService = budgetService;
        _categoryService = categoryService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
        Title = "Budgets";
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

            Budgets = await _budgetService.GetActiveAsync(userId.Value, CurrentMonth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading budgets");
            SetError("Failed to load budgets");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddBudgetAsync()
    {
        await _navigationService.NavigateToAsync("///AddBudget");
    }

    [RelayCommand]
    private async Task EditBudgetAsync(BudgetDto budget)
    {
        await _navigationService.NavigateToAsync($"///EditBudget?id={budget.Id}");
    }

    [RelayCommand]
    private async Task DeleteBudgetAsync(BudgetDto budget)
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Delete Budget",
            $"Are you sure you want to delete '{budget.Name}'?",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            await _budgetService.DeleteAsync(budget.Id, userId.Value);
            Budgets = Budgets.Where(b => b.Id != budget.Id).ToList();
            await _dialogService.ShowToastAsync("Budget deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting budget");
            SetError("Failed to delete budget");
        }
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        CurrentMonth = CurrentMonth.AddMonths(-1);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        CurrentMonth = CurrentMonth.AddMonths(1);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task CurrentMonthAsync()
    {
        CurrentMonth = DateTime.Today;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }
}