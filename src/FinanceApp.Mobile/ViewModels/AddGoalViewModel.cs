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
    private readonly GoalHistoryStore _historyStore;
    private readonly ILogger<AddGoalViewModel> _logger;

    private string? _originalName;
    private Money? _originalTargetAmount;
    private DateTime? _originalTargetDate;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Money _targetAmount = Money.Zero();

    [ObservableProperty]
    private string _amountText = string.Empty;

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
        GoalHistoryStore historyStore,
        ILogger<AddGoalViewModel> logger)
    {
        _goalService = goalService;
        _accountService = accountService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _historyStore = historyStore;
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
        if (goal == null)
        {
            _logger.LogWarning("Goal {GoalId} was not found for user {UserId}", goalId, userId.Value);
            SetError("Goal not found");
            return;
        }

        Name = goal.Name;
        TargetAmount = goal.TargetAmount;
        AmountText = goal.TargetAmount.Amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        TargetDate = goal.TargetDate;
        StartDate = goal.StartDate;
        Description = goal.Description ?? string.Empty;
        Icon = goal.Icon ?? "🎯";
        Color = goal.Color ?? "#512BD4";
        _originalName = goal.Name;
        _originalTargetAmount = goal.TargetAmount;
        _originalTargetDate = goal.TargetDate;

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
                await _historyStore.AddAsync(userId.Value, Name, GoalHistoryStore.EditedAction, BuildChangeDetails());
                await _dialogService.ShowSuccessAsync("Goal updated");
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
                await _dialogService.ShowSuccessAsync("Goal created");
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

    [RelayCommand]
    private void SelectIcon(string icon)
    {
        if (!string.IsNullOrWhiteSpace(icon))
            Icon = icon;
    }

    [RelayCommand]
    private void SelectColor(string color)
    {
        if (!string.IsNullOrWhiteSpace(color))
            Color = color;
    }

    private string? BuildChangeDetails()
    {
        var changes = new List<string>();

        if (_originalName != null && !string.Equals(_originalName, Name, StringComparison.Ordinal))
            changes.Add($"Name: {_originalName} → {Name}");

        if (_originalTargetAmount is { } originalTarget && originalTarget.Amount != TargetAmount.Amount)
            changes.Add($"Target: {originalTarget} → {TargetAmount}");

        if (_originalTargetDate is { } originalDate && originalDate.Date != TargetDate.Date)
            changes.Add($"Date: {originalDate:MMM dd, yyyy} → {TargetDate:MMM dd, yyyy}");

        return changes.Count == 0 ? null : string.Join("; ", changes);
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            SetError("Goal name is required");
            return false;
        }

        if (!decimal.TryParse(AmountText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsedAmount) || parsedAmount <= 0)
        {
            SetError("Target amount must be greater than zero");
            return false;
        }

        TargetAmount = new Money(parsedAmount, TargetAmount.Currency);

        if (TargetDate <= DateTime.Today)
        {
            SetError("Target date must be in the future");
            return false;
        }

        return true;
    }
}