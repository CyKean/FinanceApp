namespace FinanceApp.Infrastructure.Services;

using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Notifications;
using Microsoft.Extensions.Logging;

/// <summary>
/// Application-layer notification publisher. Alerts are raised in-process and
/// picked up by the host (the MAUI app forwards them into the shared feed and
/// surfaces them to the user). Platform delivery is intentionally a no-op so the
/// same service can run in tests and background workers.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public event Action<AppNotification>? AlertRaised;

    public Task PublishAsync(AppNotification notification, CancellationToken cancellationToken = default)
    {
        if (notification is null || string.IsNullOrWhiteSpace(notification.Id))
            return Task.CompletedTask;

        _logger.LogInformation(
            "Raised notification {NotificationId} ({Severity}) for group {Group}: {Title}",
            notification.Id,
            notification.Severity,
            notification.Group,
            notification.Title);

        try
        {
            AlertRaised?.Invoke(notification);
        }
        catch (Exception ex)
        {
            // A failing listener must never break the caller's save path.
            _logger.LogWarning(ex, "Notification listener failed for {NotificationId}", notification.Id);
        }

        return Task.CompletedTask;
    }

    public Task<bool> HasPermissionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task RequestPermissionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("In-app notifications do not need an OS permission");
        return Task.CompletedTask;
    }
}