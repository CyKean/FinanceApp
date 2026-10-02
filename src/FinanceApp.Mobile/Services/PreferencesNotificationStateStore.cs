namespace FinanceApp.Mobile.Services;

using FinanceApp.Application.Interfaces;

/// <summary>
/// Notification read/dismissed/announced state backed by
/// <see cref="Preferences"/>. Without this the feed would re-alert the user for
/// problems they have already acknowledged on every launch.
/// </summary>
public sealed class PreferencesNotificationStateStore : INotificationStateStore
{
    private const string DismissedKey = "notifications.dismissed";
    private const string ReadKey = "notifications.read";
    private const string SurfacedKey = "notifications.surfaced";

    public Task<HashSet<string>> LoadDismissedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Read(DismissedKey));

    public Task<HashSet<string>> LoadReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Read(ReadKey));

    public Task<HashSet<string>> LoadSurfacedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Read(SurfacedKey));

    public Task SaveAsync(
        IReadOnlyCollection<string> dismissed,
        IReadOnlyCollection<string> read,
        IReadOnlyCollection<string> surfaced,
        CancellationToken cancellationToken = default)
    {
        Write(DismissedKey, dismissed);
        Write(ReadKey, read);
        Write(SurfacedKey, surfaced);
        return Task.CompletedTask;
    }

    private static HashSet<string> Read(string key)
    {
        var raw = Preferences.Get(key, string.Empty);
        if (string.IsNullOrEmpty(raw))
            return new HashSet<string>(StringComparer.Ordinal);

        return new HashSet<string>(raw.Split(',', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
    }

    private static void Write(string key, IReadOnlyCollection<string> values) =>
        Preferences.Set(key, string.Join(',', values));
}