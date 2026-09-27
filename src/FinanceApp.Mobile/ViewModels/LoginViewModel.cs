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

    [ObservableProperty]
    private bool _isRememberMe = true;

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
        BusyMessage = IsRegisterMode ? "Creating your account..." : "Logging in...";
        ClearError();
        _authService.RememberMe = IsRememberMe;

        // Minimum visible loading time so the busy state is perceptible
        // (auth currently resolves locally; real network calls will take longer).
        var minBusyDelay = Task.Delay(1200);

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

            await minBusyDelay;

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
            BusyMessage = "Please wait...";
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

    /// <summary>
    /// Called when the login page appears: a remembered session skips login.
    /// Returns true when navigation away happened.
    /// </summary>
    public async Task<bool> CheckSavedSessionAsync()
    {
        try
        {
            if (await _authService.IsAuthenticatedAsync())
            {
                await _navigationService.NavigateToAsync("//Dashboard");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Saved session check failed");
        }

        return false;
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