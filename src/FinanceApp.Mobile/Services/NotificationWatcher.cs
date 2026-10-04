namespace FinanceApp.Mobile.Services;

using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Keeps the notification feed current and tells the user when something
/// matters.
/// <para>
/// The feed is rebuilt from live finance data on demand (app start, dashboard
/// load, pull-to-refresh) and whenever an application service raises an alert,
/// so a budget crossing its limit is announced immediately instead of waiting
/// for the user to open the notifications page. Alerts are announced once -
/// the centre remembers what has already been surfaced.
/// </para>
/// </summary>
public sealed class NotificationWatcher : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly IAuthenticationService _authService;
    private readonly IServiceProvider _services;
    private readonly INotificationService _notificationService;
    private readonly INotificationCenter _center;
    private readonly ToastService _toasts;
    private readonly ILogger<NotificationWatcher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CancellationTokenSource? _polling;
    private bool _disposed;

    public NotificationWatcher(
        IAuthenticationService authService,
        IServiceProvider services,
        INotificationService notificationService,
        INotificationCenter center,
        ToastService toasts,
        ILogger<NotificationWatcher> logger)
    {
        _authService = authService;
        _services = services;
        _notificationService = notificationService;
        _center = center;
        _toasts = toasts;
        _logger = logger;

        _notificationService.AlertRaised += OnAlertRaised;
    }

    /// <summary>Rebuilds the whole feed and announces anything newly raised.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!await TryEnterAsync())
            return;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync(cancellationToken);
            if (!userId.HasValue)
                return;

            await _center.HydrateAsync(cancellationToken);

            // Scoped per refresh, same as the sync worker, so the feed never
            // holds a DbContext for the lifetime of the app.
            //
            // And on a worker, because building the feed runs the prediction
            // pipeline, which is dozens of SQLite reads. Awaited inline this
            // blocked the UI thread on every dashboard load - the dashboard calls
            // RefreshAsync at the end of its own load - as well as on the
            // five-minute poll.
            var items = await Task.Run(async () =>
            {
                using var scope = _services.CreateScope();
                var feedBuilder = scope.ServiceProvider.GetRequiredService<INotificationFeedBuilder>();
                return await feedBuilder.BuildAsync(userId.Value, cancellationToken);
            }, cancellationToken);

            Announce(_center.Publish(items));
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer refresh.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not refresh notifications");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Starts the background refresh loop that keeps the badge honest.</summary>
    public void Start()
    {
        if (_polling is not null || _disposed)
            return;

        var cts = new CancellationTokenSource();
        _polling = cts;
        _ = PollAsync(cts.Token);
    }

    /// <summary>Stops the background loop (sign-out, teardown).</summary>
    public void Stop()
    {
        Cancel(ref _polling);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _notificationService.AlertRaised -= OnAlertRaised;
        Cancel(ref _polling);
        _gate.Dispose();
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await RefreshAsync(cancellationToken);
        }
    }

    private void OnAlertRaised(AppNotification notification)
    {
        // Raised from background/save paths, so hop to the UI thread before
        // touching bindings or the toast host.
        MainThread.BeginInvokeOnMainThread(() => _ = MergeAlertAsync(notification));
    }

    private async Task MergeAlertAsync(AppNotification notification)
    {
        if (_disposed)
            return;

        await _center.HydrateAsync();

        try
        {
            // Merge rather than replace so an alert never wipes the rest of the feed.
            Announce(_center.Publish(_center.Items.Append(notification)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not add notification {NotificationId}", notification.Id);
        }
    }

    /// <summary>
    /// Surfaces the most severe new alert as a toast. Only one is shown at a
    /// time so a burst of problems does not bury the screen in cards.
    /// </summary>
    private void Announce(IReadOnlyList<AppNotification> arrived)
    {
        var alert = arrived
            .Where(i => i.NeedsAttention)
            .OrderByDescending(i => i.Severity)
            .FirstOrDefault();

        if (alert is null)
            return;

        var extra = arrived.Count(i => i.NeedsAttention) - 1;
        var message = extra > 0 ? $"{alert.Title}  (+{extra} more)" : alert.Title;

        _toasts.ShowAsync(
            message,
            alert.Severity == NotificationSeverity.Critical ? ToastKind.Error : ToastKind.Info);
    }

    private async Task<bool> TryEnterAsync()
    {
        try
        {
            return await _gate.WaitAsync(0);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private void Cancel(ref CancellationTokenSource? source)
    {
        var current = source;
        source = null;

        if (current is null)
            return;

        try
        {
            current.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already gone.
        }
        finally
        {
            current.Dispose();
        }
    }
}