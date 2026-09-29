namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class AddAccountViewModel : BaseViewModel
{
    private readonly IAccountService _accountService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly AccountHistoryStore _historyStore;
    private readonly ILogger<AddAccountViewModel> _logger;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private AccountType _type = AccountType.Cash;

    [ObservableProperty]
    private Money _initialBalance = Money.Zero();

    [ObservableProperty]
    private string _initialBalanceText = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _icon = string.Empty;

    [ObservableProperty]
    private string _color = "#0E6B4F";

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private Guid? _editingAccountId;

    public IReadOnlyList<AccountType> AccountTypes { get; } =
        Enum.GetValues<AccountType>();

    public IReadOnlyList<string> IconChoices => Helpers.EmojiPalette.Icons;

    public IReadOnlyList<string> ColorChoices => Helpers.ColorPalette.Swatches;

    public AddAccountViewModel(
        IAccountService accountService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        AccountHistoryStore historyStore,
        ILogger<AddAccountViewModel> logger)
    {
        _accountService = accountService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _historyStore = historyStore;
        _logger = logger;
    }

    public async Task InitializeAsync(Guid? accountId = null)
    {
        IsEditing = accountId.HasValue;
        EditingAccountId = accountId;
        Title = IsEditing ? "Edit Account" : "Add Account";

        if (IsEditing && accountId.HasValue)
        {
            await LoadAccountAsync(accountId.Value);
        }
    }

    private async Task LoadAccountAsync(Guid accountId)
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue)
        {
            SetError("Could not verify your session. Please sign in again.");
            return;
        }

        var account = await _accountService.GetByIdAsync(accountId, userId.Value);
        if (account == null)
        {
            SetError("Account not found. It may have been deleted.");
            return;
        }

        Name = account.Name;
        Type = account.Type;
        InitialBalance = account.Balance;
        InitialBalanceText = account.Balance.Amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        Description = account.Description ?? string.Empty;
        Icon = account.Icon ?? string.Empty;
        Color = account.Color ?? "#0E6B4F";
        IsDefault = account.IsDefault;
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

            if (string.IsNullOrWhiteSpace(Icon))
                Icon = "💰";

            if (IsEditing && EditingAccountId.HasValue)
            {
                var updateDto = new UpdateAccountDto(
                    Name,
                    Type,
                    Description,
                    Icon,
                    Color,
                    IsDefault,
                    null);

                await _accountService.UpdateAsync(EditingAccountId.Value, updateDto, userId.Value);
                await _historyStore.AddAsync(userId.Value, Name, AccountHistoryStore.EditedAction, "Account details updated");
                await _dialogService.ShowSuccessAsync("Account updated");
            }
            else
            {
                var createDto = new CreateAccountDto(
                    Name,
                    Type,
                    InitialBalance,
                    Description,
                    Icon,
                    Color,
                    IsDefault);

                await _accountService.CreateAsync(createDto, userId.Value);
                await _historyStore.AddAsync(userId.Value, Name, AccountHistoryStore.CreatedAction, BuildDetails());
                await _dialogService.ShowSuccessAsync("Account added");
            }

            await _navigationService.NavigateToAsync("//Accounts");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving account");
            await _dialogService.ShowFailureAsync("Failed to save account");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await _navigationService.NavigateToAsync("//Accounts");
    }

    [RelayCommand]
    private void SelectIcon(string icon)
    {
        if (!string.IsNullOrEmpty(icon))
            Icon = icon;
    }

    [RelayCommand]
    private void SelectColor(string color)
    {
        if (!string.IsNullOrEmpty(color))
            Color = color;
    }

    private string BuildDetails() =>
        $"{Type} · {InitialBalance.Amount:N2} {InitialBalance.Currency}";

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            SetError("Account name is required");
            return false;
        }

        if (!decimal.TryParse(InitialBalanceText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsedBalance) || parsedBalance < 0)
        {
            SetError("Initial balance must be zero or more");
            return false;
        }

        InitialBalance = new Money(parsedBalance, InitialBalance?.Currency ?? "PHP");
        return true;
    }
}