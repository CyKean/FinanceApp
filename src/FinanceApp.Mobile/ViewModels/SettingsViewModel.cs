namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Mobile.Services;
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
    private string _appVersion = "1.0.0";

    public SettingsViewModel(
        IAuthenticationService authService,
        ISyncService syncService,
        IConnectivityService connectivityService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<SettingsViewModel> logger)
    {
        _authService = authService;
        _syncService = syncService;
        _connectivityService = connectivityService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
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
            else
            {
                await _dialogService.ShowAlertAsync("Sync Failed", result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sync");
            await _dialogService.ShowAlertAsync("Sync Error", ex.Message);
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

    [RelayCommand]
    private async Task ForceSyncAsync()
    {
        if (IsSyncing) return;

        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue) return;

        IsSyncing = true;

        try
        {
            await _syncService.ForceSyncAsync(userId.Value);
            var result = await _syncService.SyncAsync(userId.Value);
            SyncStatus = await _syncService.GetStatusAsync(userId.Value);

            if (result.Success)
            {
                await _dialogService.ShowToastAsync($"Force sent {result.SyncedCount}, received {result.PulledCount}");
            }
            else
            {
                await _dialogService.ShowAlertAsync("Force Sync Failed", result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during force sync");
            await _dialogService.ShowAlertAsync("Force Sync Error", ex.Message);
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
}