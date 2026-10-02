using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Notifications;
using FinanceApp.Application.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceApp.UnitTests;

public class NotificationCenterTests
{
    private readonly InMemoryNotificationStateStore _store = new();
    private readonly NotificationCenter _center;

    public NotificationCenterTests()
    {
        _center = new NotificationCenter(_store, Mock.Of<ILogger<NotificationCenter>>());
    }

    [Fact]
    public async Task Publish_ReportsNewlyRaisedAlerts_OnlyOnce()
    {
        await _center.HydrateAsync();
        var alert = Alert("budget-over-1", NotificationSeverity.Critical);

        var first = _center.Publish(new[] { alert });
        var second = _center.Publish(new[] { alert });

        Assert.Same(alert, Assert.Single(first));
        Assert.Empty(second);
    }

    [Fact]
    public async Task Publish_OrdersFeed_NewestFirst()
    {
        await _center.HydrateAsync();

        var older = Alert("a", NotificationSeverity.Info) with { CreatedAt = DateTime.Now.AddHours(-2) };
        var newer = Alert("b", NotificationSeverity.Critical) with { CreatedAt = DateTime.Now };

        _center.Publish(new[] { older, newer });

        Assert.Equal(new[] { "b", "a" }, _center.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Publish_CollapsesDuplicateIds()
    {
        await _center.HydrateAsync();

        _center.Publish(new[] { Alert("sync-failed", NotificationSeverity.Critical) });

        Assert.Single(_center.Items);
    }

    [Fact]
    public async Task Publish_CountsUnread()
    {
        await _center.HydrateAsync();

        _center.Publish(new[]
        {
            Alert("a", NotificationSeverity.Critical),
            Alert("b", NotificationSeverity.Info)
        });

        Assert.Equal(2, _center.UnreadCount);
        Assert.True(_center.HasNotifications);
    }

    [Fact]
    public async Task Publish_KeepsReadStateAcrossRefresh()
    {
        await _center.HydrateAsync();
        var alert = Alert("budget-near-1", NotificationSeverity.Warning);

        _center.Publish(new[] { alert });
        _center.MarkRead("budget-near-1");

        _center.Publish(new[] { alert });

        Assert.Equal(0, _center.UnreadCount);
        Assert.False(_center.Items.Single().IsUnread);
    }

    [Fact]
    public async Task Dismiss_HidesAlertAcrossRefresh()
    {
        await _center.HydrateAsync();
        var alert = Alert("budget-near-1", NotificationSeverity.Warning);

        _center.Publish(new[] { alert });
        _center.Dismiss("budget-near-1");

        _center.Publish(new[] { alert });

        Assert.Empty(_center.Items);
        Assert.Equal(0, _center.UnreadCount);
        Assert.False(_center.HasNotifications);
    }

    [Fact]
    public async Task ClearAll_EmptiesFeedAndSuppressesFuturePublish()
    {
        await _center.HydrateAsync();
        var alert = Alert("budget-near-1", NotificationSeverity.Warning);

        _center.Publish(new[] { alert });
        _center.ClearAll();
        _center.Publish(new[] { alert });

        Assert.Empty(_center.Items);
    }

    [Fact]
    public async Task MarkAllRead_ClearsBadge()
    {
        await _center.HydrateAsync();
        _center.Publish(new[]
        {
            Alert("a", NotificationSeverity.Critical),
            Alert("b", NotificationSeverity.Warning)
        });

        _center.MarkAllRead();

        Assert.Equal(0, _center.UnreadCount);
        Assert.All(_center.Items, i => Assert.False(i.IsUnread));
    }

    [Fact]
    public async Task Changed_FiresOnEveryMutation()
    {
        await _center.HydrateAsync();
        var raised = 0;
        _center.Changed += () => raised++;

        _center.Publish(new[] { Alert("a", NotificationSeverity.Info) });
        _center.MarkRead("a");
        _center.Dismiss("a");

        Assert.Equal(3, raised);
    }

    [Fact]
    public async Task HydrateAsync_RestoresDismissedAndReadState()
    {
        _store.Dismissed.Add("gone");
        _store.Read.Add("seen");

        await _center.HydrateAsync();
        _center.Publish(new[]
        {
            Alert("gone", NotificationSeverity.Critical),
            Alert("seen", NotificationSeverity.Critical)
        });

        Assert.Equal(new[] { "seen" }, _center.Items.Select(i => i.Id));
        Assert.Equal(0, _center.UnreadCount);
    }

    [Fact]
    public async Task HydrateAsync_KeepsWorking_WhenStoreThrows()
    {
        var center = new NotificationCenter(new ThrowingStore(), Mock.Of<ILogger<NotificationCenter>>());

        await center.HydrateAsync();
        center.Publish(new[] { Alert("a", NotificationSeverity.Critical) });

        Assert.Equal(1, center.UnreadCount);
    }

    [Fact]
    public async Task HydrateAsync_OnlyLoadsOnce()
    {
        var loads = 0;
        var store = new CountingStore(() => loads++);

        var center = new NotificationCenter(store, Mock.Of<ILogger<NotificationCenter>>());
        await center.HydrateAsync();
        await center.HydrateAsync();

        Assert.Equal(1, loads);
    }

    [Fact]
    public void Publish_DoesNotRecount_UnreadForReadAlerts()
    {
        _store.Read.Add("seen");
        _center.HydrateAsync().GetAwaiter().GetResult();

        _center.Publish(new[] { Alert("seen", NotificationSeverity.Info) });

        Assert.Equal(0, _center.UnreadCount);
    }

    private static AppNotification Alert(string id, NotificationSeverity severity) =>
        new(id, "Budgets", $"{id} title", $"{id} body", "alertCircle", severity, DateTime.Now, "//Budgets");

    private sealed class InMemoryNotificationStateStore : INotificationStateStore
    {
        public HashSet<string> Dismissed { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Read { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Surfaced { get; } = new(StringComparer.Ordinal);

        public Task<HashSet<string>> LoadDismissedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new HashSet<string>(Dismissed, StringComparer.Ordinal));

        public Task<HashSet<string>> LoadReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new HashSet<string>(Read, StringComparer.Ordinal));

        public Task<HashSet<string>> LoadSurfacedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new HashSet<string>(Surfaced, StringComparer.Ordinal));

        public Task SaveAsync(
            IReadOnlyCollection<string> dismissed,
            IReadOnlyCollection<string> read,
            IReadOnlyCollection<string> surfaced,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingStore : INotificationStateStore
    {
        public Task<HashSet<string>> LoadDismissedAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("storage offline");

        public Task<HashSet<string>> LoadReadAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("storage offline");

        public Task<HashSet<string>> LoadSurfacedAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("storage offline");

        public Task SaveAsync(
            IReadOnlyCollection<string> dismissed,
            IReadOnlyCollection<string> read,
            IReadOnlyCollection<string> surfaced,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("storage offline");
    }

    private sealed class CountingStore : INotificationStateStore
    {
        private readonly Action _onLoad;

        public CountingStore(Action onLoad) => _onLoad = onLoad;

        public Task<HashSet<string>> LoadDismissedAsync(CancellationToken cancellationToken = default)
        {
            _onLoad();
            return Task.FromResult(new HashSet<string>(StringComparer.Ordinal));
        }

        public Task<HashSet<string>> LoadReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new HashSet<string>(StringComparer.Ordinal));

        public Task<HashSet<string>> LoadSurfacedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new HashSet<string>(StringComparer.Ordinal));

        public Task SaveAsync(
            IReadOnlyCollection<string> dismissed,
            IReadOnlyCollection<string> read,
            IReadOnlyCollection<string> surfaced,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}