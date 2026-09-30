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
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromSeconds(10);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SyncBackgroundService> _logger;
    private readonly SemaphoreSlim _signal = new(0, 1);

    public SyncBackgroundService(IServiceProvider serviceProvider, ILogger<SyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync background worker started");
        FinanceApp.Application.SyncNotifications.Subscribe(OnDataChanged);

        try
        {
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
                // Wake immediately when local data changes (debounced to batch
                // rapid saves), otherwise tick on the regular interval.
                var signaled = await WaitForSignalOrTimeoutAsync(stoppingToken);
                if (signaled)
                {
                    DrainSignals();
                    try
                    {
                        await Task.Delay(SaveDebounce, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    DrainSignals();
                }

                try
                {
                    await RunSyncOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background sync tick failed");
                }
            }
        }
        finally
        {
            FinanceApp.Application.SyncNotifications.Unsubscribe(OnDataChanged);
            _logger.LogInformation("Sync background worker stopped");
        }
    }

    private void OnDataChanged()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // A wake-up is already pending.
        }
    }

    private void DrainSignals()
    {
        while (_signal.Wait(0))
        {
        }
    }

    private async Task<bool> WaitForSignalOrTimeoutAsync(CancellationToken stoppingToken)
    {
        var signalTask = _signal.WaitAsync(stoppingToken);
        var delayTask = Task.Delay(Interval, stoppingToken);
        var completed = await Task.WhenAny(signalTask, delayTask);
        return completed == signalTask;
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
