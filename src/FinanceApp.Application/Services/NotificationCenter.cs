namespace FinanceApp.Application.Services;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Notifications;
using Microsoft.Extensions.Logging;

/// <summary>
/// In-memory notification feed. Alerts are derived from live finance data
/// (budgets, goals, recurring bills, prediction insights, sync status) by
/// <see cref="NotificationFeedBuilder"/>, so only the read/dismissed state needs
/// storing - and that is delegated to <see cref="INotificationStateStore"/> so
/// it survives app restarts instead of re-alerting every launch.
/// </summary>
public sealed class NotificationCenter : INotificationCenter, INotifyPropertyChanged, IDisposable
{
    /// <summary>Cap on remembered ids so storage never grows without bound.</summary>
    private const int MaxRememberedIds = 300;

    private readonly INotificationStateStore _store;
    private readonly ILogger<NotificationCenter> _logger;
    private readonly HashSet<string> _dismissed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _read = new(StringComparer.Ordinal);
    private readonly HashSet<string> _surfaced = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _hydrateGate = new(1, 1);

    private int _unreadCount;
    private bool _hydrated;
    private bool _disposed;

    public NotificationCenter(INotificationStateStore store, ILogger<NotificationCenter> logger)
    {
        _store = store;
        _logger = logger;
    }

    public ObservableCollection<AppNotification> Feed { get; } = new();

    public IReadOnlyList<AppNotification> Items => Feed;

    /// <summary>Alerts already announced to the user (drives one-shot toasts).</summary>
    public IReadOnlyCollection<string> Surfaced => _surfaced;

    public int UnreadCount
    {
        get => _unreadCount;
        private set
        {
            if (_unreadCount == value)
                return;

            _unreadCount = value;
            OnPropertyChanged();
        }
    }

    public bool HasNotifications => Feed.Count > 0;

    public event Action? Changed;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Loads remembered read/dismissed state. Safe to call repeatedly.</summary>
    public async Task HydrateAsync(CancellationToken cancellationToken = default)
    {
        if (_hydrated)
            return;

        await _hydrateGate.WaitAsync(cancellationToken);
        try
        {
            if (_hydrated)
                return;

            _dismissed.UnionWith(await _store.LoadDismissedAsync(cancellationToken));
            _read.UnionWith(await _store.LoadReadAsync(cancellationToken));
            _surfaced.UnionWith(await _store.LoadSurfacedAsync(cancellationToken));
            _hydrated = true;
        }
        catch (Exception ex)
        {
            // Never let a storage hiccup stop the feed from working.
            _logger.LogWarning(ex, "Could not restore notification read state");
            _hydrated = true;
        }
        finally
        {
            _hydrateGate.Release();
        }
    }

    public IReadOnlyList<AppNotification> Publish(IEnumerable<AppNotification> incoming)
    {
        var ordered = incoming
            .Where(i => !_dismissed.Contains(i.Id))
            .GroupBy(i => i.Id, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(i => i.CreatedAt).First())
            .OrderByDescending(i => i.CreatedAt)
            .ToList();

        var known = new HashSet<string>(Feed.Select(i => i.Id), StringComparer.Ordinal);

        // Only alerts that were never in the feed and were never announced before
        // are surfaced, so a refresh never re-toasts the same problem.
        var arrived = ordered
            .Where(i => !known.Contains(i.Id) && !_surfaced.Contains(i.Id))
            .ToList();

        foreach (var item in arrived)
            _surfaced.Add(item.Id);

        Feed.Clear();
        foreach (var item in ordered)
            Feed.Add(_read.Contains(item.Id) ? item with { IsUnread = false } : item);

        Recount();
        Changed?.Invoke();

        if (arrived.Count > 0)
            Persist();

        return arrived;
    }

    public void MarkRead(string id)
    {
        var index = IndexOf(id);
        if (index < 0 || !Feed[index].IsUnread)
            return;

        Feed[index] = Feed[index] with { IsUnread = false };
        _read.Add(id);
        Recount();
        Changed?.Invoke();
        Persist();
    }

    public void MarkAllRead()
    {
        if (UnreadCount == 0)
            return;

        for (var i = 0; i < Feed.Count; i++)
        {
            if (!Feed[i].IsUnread)
                continue;

            Feed[i] = Feed[i] with { IsUnread = false };
            _read.Add(Feed[i].Id);
        }

        Recount();
        Changed?.Invoke();
        Persist();
    }

    public void Dismiss(string id)
    {
        var index = IndexOf(id);
        if (index < 0)
            return;

        Feed.RemoveAt(index);
        _dismissed.Add(id);
        _read.Remove(id);
        _surfaced.Remove(id);
        Recount();
        Changed?.Invoke();
        Persist();
    }

    public void ClearAll()
    {
        if (Feed.Count == 0)
            return;

        foreach (var item in Feed)
        {
            _dismissed.Add(item.Id);
            _read.Remove(item.Id);
        }

        Feed.Clear();
        Recount();
        Changed?.Invoke();
        Persist();
    }

    public void Reset()
    {
        _dismissed.Clear();
        _read.Clear();
        _surfaced.Clear();
        Feed.Clear();
        Recount();
        Changed?.Invoke();
        Persist();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Changed = null;
        PropertyChanged = null;
        _hydrateGate.Dispose();
    }

    private int IndexOf(string id)
    {
        for (var i = 0; i < Feed.Count; i++)
        {
            if (Feed[i].Id == id)
                return i;
        }

        return -1;
    }

    private void Recount() => UnreadCount = Feed.Count(i => i.IsUnread);

    private void Persist() => _ = PersistAsync();

    private async Task PersistAsync()
    {
        try
        {
            await _store.SaveAsync(
                Trim(_dismissed),
                Trim(_read),
                Trim(_surfaced),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist notification read state");
        }
    }

    private static string[] Trim(HashSet<string> source) =>
        source.Count <= MaxRememberedIds
            ? source.ToArray()
            : source.TakeLast(MaxRememberedIds).ToArray();

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}