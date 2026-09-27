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
    private readonly ILogger<AccountsViewModel> _logger;

    [ObservableProperty]
    private IReadOnlyList<AccountDto> _accounts = Array.Empty<AccountDto>();

    [ObservableProperty]
    private Money _totalBalance = Money.Zero();

    public AccountsViewModel(
        IAccountService accountService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<AccountsViewModel> logger)
    {
        _accountService = accountService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
        Title = "Accounts";
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

            Accounts = await _accountService.GetAllAsync(userId.Value);
            TotalBalance = await _accountService.GetTotalBalanceAsync(userId.Value);
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
    private async Task DeleteAccountAsync(AccountDto account)
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Delete Account",
            $"Are you sure you want to delete '{account.Name}'?",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            await _accountService.DeleteAsync(account.Id, userId.Value);
            Accounts = Accounts.Where(a => a.Id != account.Id).ToList();
            TotalBalance = await _accountService.GetTotalBalanceAsync(userId.Value);
            await _dialogService.ShowToastAsync("Account deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting account");
            if (ex is FinanceApp.Domain.Exceptions.ValidationException vex) await _dialogService.ShowErrorToastAsync(vex.Message);
            else await _dialogService.ShowToastAsync("Delete failed. Please try again.");
        }
    }

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
