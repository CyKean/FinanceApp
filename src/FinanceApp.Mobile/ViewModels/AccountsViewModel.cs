namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class AccountsViewModel : BaseViewModel
{
    private readonly IAccountService _accountService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly AccountHistoryStore _historyStore;
    private readonly ILogger<AccountsViewModel> _logger;

    [ObservableProperty]
    private IReadOnlyList<AccountDto> _accounts = Array.Empty<AccountDto>();

    [ObservableProperty]
    private IReadOnlyList<AccountHistoryEntryDto> _history = Array.Empty<AccountHistoryEntryDto>();

    [ObservableProperty]
    private bool _showHistory;

    [ObservableProperty]
    private Money _totalBalance = Money.Zero();

    public bool IsAccountsTab => !ShowHistory;

    public Func<object, Task>? AnimateDeleteAsync { get; set; }

    public AccountsViewModel(
        IAccountService accountService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        AccountHistoryStore historyStore,
        ILogger<AccountsViewModel> logger)
    {
        _accountService = accountService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _historyStore = historyStore;
        _logger = logger;
        Title = "Accounts";
    }

    partial void OnShowHistoryChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAccountsTab));
    }

    [RelayCommand]
    private void ShowAccountsTab() => ShowHistory = false;

    [RelayCommand]
    private void ShowHistoryTab() => ShowHistory = true;

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

            Accounts = await _accountService.GetAllAsync(userId.Value);
            TotalBalance = await _accountService.GetTotalBalanceAsync(userId.Value);
            History = await _historyStore.GetAllAsync(userId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading accounts");
            SetError("Failed to load accounts");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        await _navigationService.NavigateToAsync("//AddAccount");
    }

    [RelayCommand]
    private async Task EditAccountAsync(AccountDto account)
    {
        await _navigationService.NavigateToAsync($"//EditAccount?id={account.Id}");
    }

    [RelayCommand]
    private async Task AccountActionsAsync(AccountDto account)
    {
        var options = new List<string> { "Edit" };
        if (!account.IsDefault)
            options.Add("Set as default");
        options.Add("Delete");

        var choice = await _dialogService.ShowChoiceSheetAsync(account.Name, options.ToArray());
        if (string.IsNullOrEmpty(choice))
            return;

        switch (choice)
        {
            case "Edit":
                await EditAccountAsync(account);
                break;
            case "Set as default":
                await SetDefaultAsync(account);
                break;
            case "Delete":
                await DeleteAccountAsync(account);
                break;
        }
    }

    [RelayCommand]
    private async Task DeleteAccountAsync(AccountDto account)
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Delete Account",
            $"Are you sure you want to delete '{account.Name}'?",
            "Delete",
            "Cancel",
            destructive: true);

        if (!confirmed) return;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            await _accountService.DeleteAsync(account.Id, userId.Value);
            await _historyStore.AddAsync(userId.Value, account.Name, AccountHistoryStore.DeletedAction, BuildDetails(account));

            if (AnimateDeleteAsync is not null)
            {
                try
                {
                    await AnimateDeleteAsync(account);
                }
                catch (Exception animEx)
                {
                    _logger.LogWarning(animEx, "Delete animation failed for account {AccountId}", account.Id);
                }
            }

            Accounts = Accounts.Where(a => a.Id != account.Id).ToList();
            TotalBalance = await _accountService.GetTotalBalanceAsync(userId.Value);
            History = await _historyStore.GetAllAsync(userId.Value);
            await _dialogService.ShowToastAsync("Account deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting account");
            if (ex is FinanceApp.Domain.Exceptions.ValidationException vex) await _dialogService.ShowFailureAsync(vex.Message);
            else await _dialogService.ShowFailureAsync("Delete failed. Please try again.");
        }
    }

    private static string BuildDetails(AccountDto account) =>
        $"{account.Type} · {account.Balance.Amount:N2} {account.Balance.Currency}";

    [RelayCommand]
    private async Task SetDefaultAsync(AccountDto account)
    {
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            await _accountService.SetDefaultAsync(account.Id, userId.Value);
            Accounts = Accounts.Select(a => a.Id == account.Id ? a with { IsDefault = true } : a with { IsDefault = false }).ToList();
            await _dialogService.ShowToastAsync($"{account.Name} set as default");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting default account");
            SetError("Failed to set default account");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }
}
