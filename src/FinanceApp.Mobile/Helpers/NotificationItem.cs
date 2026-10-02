namespace FinanceApp.Mobile.Helpers;

/// <summary>Severity of an in-app notification, used for icon + color choice.</summary>
public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Critical
}

/// <summary>A single in-app notification derived from live finance data.</summary>
public sealed record NotificationItem(
    string Id,
    string Group,
    string Title,
    string Body,
    string IconKey,
    NotificationSeverity Severity,
    DateTime CreatedAt,
    string? Destination)
{
    public bool IsUnread { get; init; } = true;

    public string TimeLabel
    {
        get
        {
            var delta = DateTime.UtcNow - CreatedAt;
            if (CreatedAt.Date == DateTime.UtcNow.Date)
            {
                if (delta.TotalMinutes < 1) return "Just now";
                if (delta.TotalMinutes < 60) return $"{delta.TotalMinutes:F0}m ago";
                return $"{delta.TotalHours:F0}h ago";
            }

            if (CreatedAt.Date == DateTime.UtcNow.Date.AddDays(-1)) return "Yesterday";
            return CreatedAt.ToString("MMM dd");
        }
    }
}