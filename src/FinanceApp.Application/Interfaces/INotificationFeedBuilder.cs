namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.Notifications;

/// <summary>
/// Derives the notification feed from live finance data. Every alert id is
/// deterministic so repeated builds de-duplicate instead of stacking.
/// </summary>
public interface INotificationFeedBuilder
{
    Task<IReadOnlyList<AppNotification>> BuildAsync(Guid userId, CancellationToken cancellationToken = default);
}