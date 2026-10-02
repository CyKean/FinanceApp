namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.Notifications;

/// <summary>
/// App-wide notification feed shared by every bell so the badge count stays in
/// sync no matter which screen the user is on. Read and dismissed state survive
/// navigation and (through <see cref="INotificationStateStore"/>) app restarts.
/// </summary>
public interface INotificationCenter
{
    IReadOnlyList<AppNotification> Items { get; }

    int UnreadCount { get; }

    bool HasNotifications { get; }

    /// <summary>Raised whenever items or read state change so pages can refresh.</summary>
    event Action? Changed;

    /// <summary>Restores remembered read/dismissed state before the first publish.</summary>
    Task HydrateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the feed with <paramref name="incoming"/>, preserving read state
    /// for items that persist and filtering anything previously dismissed.
    /// </summary>
    /// <returns>
    /// Alerts raised for the first time - never shown in the feed and never
    /// announced before. Callers use this to notify the user exactly once.
    /// </returns>
    IReadOnlyList<AppNotification> Publish(IEnumerable<AppNotification> incoming);

    void MarkRead(string id);

    void MarkAllRead();

    void Dismiss(string id);

    void ClearAll();

    /// <summary>Forgets dismissed items and read state (e.g. on sign-out).</summary>
    void Reset();
}