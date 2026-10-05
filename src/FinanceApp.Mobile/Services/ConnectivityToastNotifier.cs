namespace FinanceApp.Mobile.Services;

using FinanceApp.Application.Interfaces;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Pushes a toast when the device goes offline or comes back online. Gives the
/// user a low-effort signal that local changes created while offline are safe
/// and will sync later.
/// </summary>
public sealed class ConnectivityToastNotifier : IHostedService
{
    private readonly IConnectivityService _connectivity;
    private readonly ToastService _toast;

    public ConnectivityToastNotifier(IConnectivityService connectivity, ToastService toast)
    {
        _connectivity = connectivity;
        _toast = toast;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;
        return Task.CompletedTask;
    }

    private void OnConnectivityChanged(ConnectivityChangedEventArgs args)
    {
        if (args.CurrentAccess == NetworkAccess.None)
            _ = _toast.ShowAsync("You're offline. Changes are saved locally and sync when you reconnect.", ToastKind.Info);
        else if (args.CurrentAccess == NetworkAccess.Internet && args.PreviousAccess != NetworkAccess.Internet)
            _ = _toast.ShowAsync("Back online. Syncing is running in the background.", ToastKind.Success);
    }
}