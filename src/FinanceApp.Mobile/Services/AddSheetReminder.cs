namespace FinanceApp.Mobile.Services;

using FinanceApp.Application.Interfaces;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Lightweight reminder shown when an add/edit sheet opens: warn if the device
/// is offline, and nudge to sync when there are still unpushed changes that a
/// second device might rely on being current first.
/// </summary>
public static class AddSheetReminder
{
    public static async Task WarnIfNeededAsync()
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        if (services is null)
            return;

        var toast = services.GetService<ToastService>();
        var connectivity = services.GetService<IConnectivityService>();
        var auth = services.GetService<IAuthenticationService>();
        var sync = services.GetService<ISyncService>();
        if (toast is null || connectivity is null)
            return;

        if (connectivity.CurrentAccess != NetworkAccess.Internet)
        {
            await toast.ShowAsync("You are offline - changes stay on this device and sync later.", ToastKind.Info);
            return;
        }

        if (auth is null || sync is null)
            return;

        try
        {
            var userId = await auth.GetCurrentUserIdAsync();
            if (!userId.HasValue)
                return;

            var status = await sync.GetStatusAsync(userId.Value);
            if (status.PendingCount > 0)
                await toast.ShowAsync(
                    $"{status.PendingCount} unsynced change(s) on this device - sync first if another device might have updates.",
                    ToastKind.Info);
        }
        catch
        {
            // The reminder itself must never block the editor.
        }
    }
}