namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class AddGoalViewModel : BaseViewModel
{
    private readonly IFinancialGoalService _goalService;
    private readonly IAccountService _accountService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<AddGoalViewModel> _logger;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Money _targetAmount = Money.Zero();

    [ObservableProperty]
    private DateTime _targetDate = DateTime.Today.AddMonths(6);

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _icon = "🎯";

    [ObservableProperty]
    private string _color = "#512BD4";

    [ObservableProperty]
    private AccountDto? _linkedAccount;

    [ObservableProperty]
    private IReadOnlyList<AccountDto> _accounts = Array.Empty<AccountDto>();

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private Guid? _editingGoalId;

    public AddGoalViewModel(
        IFinancialGoalService goalService,
        IAccountService accountService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<AddGoalViewModel> logger)
    {
        _goalService = goalService;
        _accountService = accountService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
    }

    public async Task InitializeAsync(Guid? goalId = null)
    {
        IsEditing = goalId.HasValue;
        EditingGoalId = goalId;
        Title = IsEditing ? "Edit Goal" : "Add Goal";

        await LoadAccountsAsync();

        if (IsEditing && goalId.HasValue)
        {
            await LoadGoalAsync(goalId.Value);
        }
    }

    private async Task LoadAccountsAsync()
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue) return;

        Accounts = await _accountService.GetAllAsync(userId.Value);
    }

    private async Task LoadGoalAsync(Guid goalId)
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue) return;

        var goal = await _goalService.GetByIdAsync(goalId, userId.Value);
        if (goal == null) return;

        Name = goal.Name;
        TargetAmount = goal.TargetAmount;
        TargetDate = goal.TargetDate;
        StartDate = goal.StartDate;
        Description = goal.Description ?? string.Empty;
        Icon = goal.Icon ?? "🎯";
        Color = goal.Color ?? "#512BD4";

        if (goal.LinkedAccountId.HasValue)
            LinkedAccount = Accounts.FirstOrDefault(a => a.Id == goal.LinkedAccountId.Value.Value);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (!ValidateInput())
            return;

        IsBusy = true;
        ClearError();

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            if (IsEditing && EditingGoalId.HasValue)
            {
                var updateDto = new UpdateFinancialGoalDto(
                    Name,
                    TargetAmount,
                    TargetDate,
                    Description,
                    Icon,
                    Color,
                    LinkedAccount != null ? new AccountId(LinkedAccount.Id) : null,
                    null);

                await _goalService.UpdateAsync(EditingGoalId.Value, updateDto, userId.Value);
                await _dialogService.ShowToastAsync("Goal updated");
            }
            else
            {
                var createDto = new CreateFinancialGoalDto(
                    Name,
                    TargetAmount,
                    TargetDate,
                    StartDate,
                    Description,
                    Icon,
                    Color,
                    LinkedAccount != null ? new AccountId(LinkedAccount.Id) : null);

                await _goalService.CreateAsync(createDto, userId.Value);
                await _dialogService.ShowToastAsync("Goal created");
            }

            await _navigationService.NavigateToAsync("//Goals");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving goal");
            SetError("Failed to save goal");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await _navigationService.NavigateToAsync("//Goals");
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            SetError("Goal name is required");
            return false;
        }

        if (TargetAmount.Amount <= 0)
        {
            SetError("Target amount must be greater than zero");
            return false;
        }

        if (TargetDate <= DateTime.Today)
        {
            SetError("Target date must be in the future");
            return false;
        }

        return true;
    }
}