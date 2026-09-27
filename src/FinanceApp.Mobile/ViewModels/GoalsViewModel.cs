namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class GoalsViewModel : BaseViewModel
{
    private readonly IFinancialGoalService _goalService;
    private readonly IAccountService _accountService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<GoalsViewModel> _logger;

    [ObservableProperty]
    private IReadOnlyList<FinancialGoalDto> _goals = Array.Empty<FinancialGoalDto>();

    [ObservableProperty]
    private GoalStatus _filterStatus = GoalStatus.Active;

    public GoalsViewModel(
        IFinancialGoalService goalService,
        IAccountService accountService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<GoalsViewModel> logger)
    {
        _goalService = goalService;
        _accountService = accountService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
        Title = "Goals";
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

            Goals = await _goalService.GetAllAsync(userId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading goals");
            SetError("Failed to load goals");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddGoalAsync()
    {
        await _navigationService.NavigateToAsync("//AddGoal");
    }

    [RelayCommand]
    private async Task EditGoalAsync(FinancialGoalDto goal)
    {
        await _navigationService.NavigateToAsync($"//EditGoal?id={goal.Id}");
    }

    [RelayCommand]
    private async Task AddProgressAsync(FinancialGoalDto goal)
    {
        var amountStr = await _dialogService.ShowPromptAsync(
            "Add Progress",
            $"Current: {goal.CurrentAmount}\nTarget: {goal.TargetAmount}\n\nEnter amount to add:",
            "0.00",
            "Add",
            "Cancel");

        if (string.IsNullOrWhiteSpace(amountStr) || !decimal.TryParse(amountStr, out var amount))
            return;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            var progressDto = new GoalProgressDto(new Money(amount, goal.TargetAmount.Currency));
            await _goalService.AddProgressAsync(goal.Id, progressDto, userId.Value);
            await LoadAsync();
            await _dialogService.ShowToastAsync("Progress added");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding progress");
            SetError("Failed to add progress");
        }
    }

    [RelayCommand]
    private async Task CompleteGoalAsync(FinancialGoalDto goal)
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Complete Goal",
            $"Mark '{goal.Name}' as completed?",
            "Complete",
            "Cancel");

        if (!confirmed) return;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            await _goalService.CompleteAsync(goal.Id, userId.Value);
            await LoadAsync();
            await _dialogService.ShowToastAsync("Goal completed!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing goal");
            SetError("Failed to complete goal");
        }
    }

    [RelayCommand]
    private async Task DeleteGoalAsync(FinancialGoalDto goal)
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Delete Goal",
            $"Are you sure you want to delete '{goal.Name}'?",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            await _goalService.DeleteAsync(goal.Id, userId.Value);
            Goals = Goals.Where(g => g.Id != goal.Id).ToList();
            await _dialogService.ShowToastAsync("Goal deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting goal");
            if (ex is FinanceApp.Domain.Exceptions.ValidationException vex) await _dialogService.ShowErrorToastAsync(vex.Message);
            else await _dialogService.ShowToastAsync("Delete failed. Please try again.");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }
}
