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

    /// <summary>
    /// True from launch until the remembered session has been resolved.
    /// <para>
    /// Restoring a session is not instant - it reads the secure store and, if
    /// Supabase is configured, may go to the network. Without this the login form
    /// flashes up for a frame or two before the app silently redirects to the
    /// dashboard, which reads as a glitch on every cold start.
    /// </para>
    /// </summary>
    [ObservableProperty]
    private bool _isRestoringSession = true;

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
        bool authenticated;
        try
        {
            authenticated = await _authService.IsAuthenticatedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Saved session check failed");
            authenticated = false;
        }

        if (!authenticated)
        {
            IsRestoringSession = false;
            return false;
        }

        // Deliberately left true: the shell is navigating away, and flipping it
        // now would flash the login form on the way out.
        await _navigationService.NavigateToAsync("//Dashboard");
        return true;
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