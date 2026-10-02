namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.Notifications;

/// <summary>
/// Publishes alerts raised by domain/application services into the shared
/// in-app notification feed. Alerts are delivered in-process; the platform
/// permission members stay here so OS-level delivery can be added later
/// without touching callers.
/// </summary>
public interface INotificationService
{
    /// <summary>Raised when an alert is published, so hosts can surface it.</summary>
    event Action<AppNotification>? AlertRaised;

    /// <summary>
    /// Publishes <paramref name="notification"/> to the feed. Alerts must carry a
    /// deterministic <see cref="AppNotification.Id"/> so repeats are de-duplicated.
    /// </summary>
    Task PublishAsync(AppNotification notification, CancellationToken cancellationToken = default);

    /// <summary>True once the user has allowed notifications (always true in-app).</summary>
    Task<bool> HasPermissionAsync(CancellationToken cancellationToken = default);

    Task RequestPermissionAsync(CancellationToken cancellationToken = default);
}