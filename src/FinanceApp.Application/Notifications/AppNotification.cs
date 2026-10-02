namespace FinanceApp.Application.Notifications;

/// <summary>Severity of a notification, used for icon, accent colour and alerting.</summary>
public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Critical
}

/// <summary>
/// A single notification derived from live finance data. Ids are deterministic
/// (derived from the entity + condition) so the same alert can be de-duplicated
/// across refreshes instead of stacking up.
/// </summary>
public sealed record AppNotification(
    string Id,
    string Group,
    string Title,
    string Body,
    string IconKey,
    NotificationSeverity Severity,
    DateTime CreatedAt,
    string? Destination)
{
    /// <summary>Severities that should interrupt the user with a toast.</summary>
    public bool NeedsAttention => Severity is NotificationSeverity.Warning or NotificationSeverity.Critical;

    public bool IsUnread { get; init; } = true;

    public string TimeLabel => Describe(CreatedAt, DateTime.Now);

    /// <summary>True when the notification belongs to the "Today" group.</summary>
    public bool IsFromToday(DateTime today) => CreatedAt.Date >= today.Date;

    internal static string Describe(DateTime createdAt, DateTime now)
    {
        var delta = now - createdAt;

        if (createdAt.Date == now.Date)
        {
            if (delta.TotalMinutes < 1) return "Just now";
            if (delta.TotalMinutes < 60) return $"{delta.TotalMinutes:F0}m ago";
            return $"{delta.TotalHours:F0}h ago";
        }

        if (createdAt.Date == now.Date.AddDays(-1)) return "Yesterday";
        return createdAt.ToString("MMM dd");
    }
}