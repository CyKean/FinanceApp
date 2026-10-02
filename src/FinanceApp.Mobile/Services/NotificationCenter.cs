namespace FinanceApp.Mobile.Services;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FinanceApp.Mobile.Helpers;

/// <summary>
/// In-memory notification feed. The app derives notifications from live finance
/// data (budgets, goals, recurring bills, prediction insights, sync status) so
/// there is nothing to persist, but read/dismissed state is kept for the session
/// and shared through <see cref="UnreadCount"/> so any bell can show a badge.
/// </summary>
public sealed class NotificationCenter : INotifyPropertyChanged
{
    private readonly HashSet<string> _dismissed = new(StringComparer.Ordinal);
    private int _unreadCount;

    public ObservableCollection<NotificationItem> Items { get; } = new();

    public int UnreadCount
    {
        get => _unreadCount;
        private set
        {
            if (_unreadCount == value) return;
            _unreadCount = value;
            OnPropertyChanged();
        }
    }

    public bool HasNotifications => Items.Count > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised whenever items/read-state change so pages can refresh.</summary>
    public event Action? Changed;

    /// <summary>Replaces the feed, preserving read state for items that persist.</summary>
    public void Publish(IEnumerable<NotificationItem> incoming)
    {
        var ordered = incoming
            .Where(i => !_dismissed.Contains(i.Id))
            .OrderByDescending(i => i.CreatedAt)
            .ToList();

        var previouslyUnread = Items.ToDictionary(i => i.Id, i => i.IsUnread);

        Items.Clear();
        foreach (var item in ordered)
        {
            var isUnread = previouslyUnread.TryGetValue(item.Id, out var wasUnread) ? wasUnread : true;
            Items.Add(item with { IsUnread = isUnread });
        }

        Recount();
        OnPropertyChanged(nameof(HasNotifications));
        Changed?.Invoke();
    }

    public void MarkRead(string id)
    {
        var index = IndexOf(id);
        if (index < 0 || !Items[index].IsUnread) return;

        Items[index] = Items[index] with { IsUnread = false };
        Recount();
        Changed?.Invoke();
    }

    public void MarkAllRead()
    {
        if (UnreadCount == 0) return;

        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].IsUnread)
                Items[i] = Items[i] with { IsUnread = false };
        }

        Recount();
        Changed?.Invoke();
    }

    public void Dismiss(string id)
    {
        if (IndexOf(id) < 0) return;

        Items.RemoveAt(IndexOf(id));
        _dismissed.Add(id);
        Recount();
        OnPropertyChanged(nameof(HasNotifications));
        Changed?.Invoke();
    }

    public void ClearAll()
    {
        foreach (var item in Items)
            _dismissed.Add(item.Id);

        Items.Clear();
        Recount();
        OnPropertyChanged(nameof(HasNotifications));
        Changed?.Invoke();
    }

    private int IndexOf(string id)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Id == id) return i;
        }
        return -1;
    }

    private void Recount() => UnreadCount = Items.Count(i => i.IsUnread);

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}