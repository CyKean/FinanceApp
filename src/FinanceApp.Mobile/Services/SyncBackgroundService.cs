namespace FinanceApp.Mobile.Services;

using AppNetworkAccess = FinanceApp.Application.Interfaces.NetworkAccess;
using FinanceApp.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Offline-first engine room: every minute, if a user is signed in and the
/// device is online, drains the local outbox to Supabase and pulls remote
/// changes back into SQLite. The UI only ever reads SQLite.
/// </summary>
public class SyncBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SyncBackgroundService> _logger;

    public SyncBackgroundService(IServiceProvider serviceProvider, ILogger<SyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync background worker started");

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSyncOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background sync tick failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Sync background worker stopped");
    }

    private async Task RunSyncOnceAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var provider = scope.ServiceProvider;

        var authService = provider.GetRequiredService<IAuthenticationService>();
        var userId = await authService.GetCurrentUserIdAsync(stoppingToken);
        if (!userId.HasValue)
            return;

        var connectivity = provider.GetRequiredService<IConnectivityService>();
        if (await connectivity.CheckConnectivityAsync(stoppingToken) != AppNetworkAccess.Internet)
            return;

        var syncService = provider.GetRequiredService<ISyncService>();
        var result = await syncService.SyncAsync(userId.Value, stoppingToken);

        if (result.SyncedCount > 0 || !result.Success)
            _logger.LogInformation("Background sync: {Synced} synced, {Failed} failed", result.SyncedCount, result.FailedCount);
    }
}
