namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.Services.Theming;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IAuthenticationService _authService;
    private readonly ISyncService _syncService;
    private readonly IConnectivityService _connectivityService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly AppUpdatePromptService _updatePrompt;
    private readonly ThemeService _themeService;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private SyncStatusDto _syncStatus = new(false, null, 0, 0, null);

    [ObservableProperty]
    private IReadOnlyList<SyncOperationDto> _recentSyncOperations = Array.Empty<SyncOperationDto>();

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private bool _isPeriodicSyncEnabled;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private string _activeThemeName = string.Empty;

    public SettingsViewModel(
        IAuthenticationService authService,
        ISyncService syncService,
        IConnectivityService connectivityService,
        INavigationService navigationService,
        IDialogService dialogService,
        AppUpdatePromptService updatePrompt,
        ThemeService themeService,
        ILogger<SettingsViewModel> logger)
    {
        _authService = authService;
        _syncService = syncService;
        _connectivityService = connectivityService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _updatePrompt = updatePrompt;
        _themeService = themeService;
        _logger = logger;
        ActiveThemeName = _themeService.DisplayName;
        _themeService.ThemeChanged += (_, _) => ActiveThemeName = _themeService.DisplayName;
        Title = "Settings";
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
            var email = await _authService.GetCurrentUserEmailAsync();
            UserEmail = email ?? "Not logged in";

            // Read from the installed package rather than a hard-coded string, so
            // this can never drift from the version that is actually running.
            AppVersion = _updatePrompt.InstalledVersion;

            if (userId.HasValue)
            {
                SyncStatus = await _syncService.GetStatusAsync(userId.Value);
                RecentSyncOperations = await _syncService.GetRecentOperationsAsync(userId.Value);
                IsPeriodicSyncEnabled = _connectivityService.CurrentAccess == NetworkAccess.Internet;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings");
            SetError("Failed to load settings");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (IsSyncing) return;

        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue) return;

        IsSyncing = true;

        try
        {
            var result = await _syncService.SyncAsync(userId.Value);
            SyncStatus = await _syncService.GetStatusAsync(userId.Value);
            RecentSyncOperations = await _syncService.GetRecentOperationsAsync(userId.Value);

            if (result.Success)
            {
                await _dialogService.ShowToastAsync($"Sent {result.SyncedCount}, received {result.PulledCount}");
            }
            else if (result.DeferredCount > 0)
            {
                await ShowSyncPausedAsync(result.ErrorMessage);
            }
            else
            {
                var offline = (result.ErrorMessage ?? string.Empty).StartsWith("No internet", StringComparison.OrdinalIgnoreCase);
                await _dialogService.ShowErrorAsync(offline ? "No internet" : "Sync Failed", result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sync");
            await _dialogService.ShowErrorAsync("Sync Error", ex.Message);
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task TogglePeriodicSyncAsync()
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue) return;

        if (IsPeriodicSyncEnabled)
        {
            _syncService.StopPeriodicSync();
            IsPeriodicSyncEnabled = false;
            await _dialogService.ShowToastAsync("Periodic sync disabled");
        }
        else
        {
            _syncService.StartPeriodicSync(userId.Value);
            IsPeriodicSyncEnabled = true;
            await _dialogService.ShowToastAsync("Periodic sync enabled (every 5 minutes)");
        }
    }

    private async Task ShowSyncPausedAsync(string? errorMessage)
    {
        if (await _authService.IsEmailVerificationRequiredAsync() &&
            (errorMessage ?? string.Empty).Contains("Not signed in", StringComparison.OrdinalIgnoreCase))
        {
            var send = await _dialogService.ShowConfirmationAsync(
                "Verify your email",
                "Your cloud account exists, but its email hasn't been confirmed yet. Check your inbox - want us to send the link again?",
                "Send email",
                "Not now");

            if (send)
            {
                var ok = await _authService.ResendEmailVerificationAsync();
                await _dialogService.ShowToastAsync(ok ? "Verification email sent." : "Couldn't send the email. Try again later.");
            }

            return;
        }

        await _dialogService.ShowErrorAsync("Sync Paused", errorMessage ?? "Sync will resume automatically.");
    }

    [RelayCommand]
    private async Task ForceSyncAsync()
    {
        if (IsSyncing) return;

        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue) return;

        IsSyncing = true;

        try
        {
            // ForceSyncAsync returns the result of the single sync it runs; calling
            // SyncAsync again here would repeat the whole pipeline for nothing.
            var result = await _syncService.ForceSyncAsync(userId.Value);
            SyncStatus = await _syncService.GetStatusAsync(userId.Value);

            if (result.Success)
            {
                await _dialogService.ShowToastAsync($"Force sent {result.SyncedCount}, received {result.PulledCount}");
            }
            else if (result.DeferredCount > 0)
            {
                await ShowSyncPausedAsync(result.ErrorMessage);
            }
            else
            {
                var offline = (result.ErrorMessage ?? string.Empty).StartsWith("No internet", StringComparison.OrdinalIgnoreCase);
                await _dialogService.ShowErrorAsync(offline ? "No internet" : "Force Sync Failed", result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during force sync");
            await _dialogService.ShowErrorAsync("Force Sync Error", ex.Message);
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Logout",
            "Are you sure you want to logout?",
            "Logout",
            "Cancel",
            destructive: true);

        if (!confirmed) return;

        try
        {
            _syncService.StopPeriodicSync();
            await _authService.LogoutAsync();
            await _navigationService.NavigateToAsync("//Login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            SetError("Failed to logout");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    /// <summary>
    /// Ignores the automatic check's cache and asks GitHub right now.
    /// </summary>
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingForUpdates) return;

        IsCheckingForUpdates = true;
        try
        {
            await _updatePrompt.CheckNowAsync();
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }
}