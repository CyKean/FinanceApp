namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.Interfaces;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class LoginViewModel : BaseViewModel
{
    private const int MinimumPasswordLength = 6;

    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<LoginViewModel> _logger;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

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

        try
        {
            // Both paths resolve against the on-device account store, so this
            // completes without waiting on a network round-trip.
            var result = IsRegisterMode
                ? await _authService.RegisterAsync(Email, Password)
                : await _authService.LoginAsync(Email, Password);

            if (result.Success)
            {
                Password = string.Empty;
                ConfirmPassword = string.Empty;

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
            SetError("Something went wrong. Please try again.");
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
        ConfirmPassword = string.Empty;
        ClearError();
    }

    [RelayCommand]
    private async Task ForgotPasswordAsync()
    {
        await _dialogService.ShowAlertAsync(
            "Reset Password",
            "Passwords are stored only on this device, so there is no email to send a reset link to. If you registered online before, sign in with your email and password when you have a connection.");
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

        if (!IsValidEmail(Email))
        {
            SetError("Enter a valid email address");
            return false;
        }

        if (string.IsNullOrEmpty(Password))
        {
            SetError("Password is required");
            return false;
        }

        // Only enforce the floor on registration: an account created before the
        // rule tightened must still be able to sign in.
        if (IsRegisterMode && Password.Length < MinimumPasswordLength)
        {
            SetError($"Password must be at least {MinimumPasswordLength} characters");
            return false;
        }

        if (IsRegisterMode && !string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            SetError("Passwords do not match");
            return false;
        }

        return true;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var trimmed = email.Trim();
            var address = new System.Net.Mail.MailAddress(trimmed);
            return address.Address == trimmed && address.Host.Contains('.') && !address.Host.EndsWith('.');
        }
        catch
        {
            return false;
        }
    }
}