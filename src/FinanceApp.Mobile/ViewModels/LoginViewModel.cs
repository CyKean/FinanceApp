namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.Interfaces;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<LoginViewModel> _logger;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isRegisterMode;

    public LoginViewModel(
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<LoginViewModel> logger)
    {
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
        Title = "Login";
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;

        if (!ValidateInput())
            return;

        IsBusy = true;
        ClearError();

        try
        {
            AuthResultDto result;

            if (IsRegisterMode)
            {
                result = await _authService.RegisterAsync(Email, Password);
            }
            else
            {
                result = await _authService.LoginAsync(Email, Password);
            }

            if (result.Success)
            {
                await _dialogService.ShowToastAsync(IsRegisterMode ? "Account created!" : "Welcome back!");
                await _navigationService.NavigateToAsync("//Dashboard");
            }
            else
            {
                SetError(result.ErrorMessage ?? "Authentication failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication");
            SetError("An error occurred. Please try again.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsRegisterMode = !IsRegisterMode;
        Title = IsRegisterMode ? "Register" : "Login";
        ClearError();
    }

    [RelayCommand]
    private async Task ForgotPasswordAsync()
    {
        await _dialogService.ShowAlertAsync("Reset Password", "Password reset functionality will be implemented in a future update.");
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            SetError("Email is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            SetError("Password is required");
            return false;
        }

        if (Password.Length < 6)
        {
            SetError("Password must be at least 6 characters");
            return false;
        }

        return true;
    }
}