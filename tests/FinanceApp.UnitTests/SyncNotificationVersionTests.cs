namespace FinanceApp.UnitTests;

using System.Threading;
using FinanceApp.Application;
using Xunit;

/// <summary>
/// The "don't re-query unless something changed" check is one integer compared
/// against a counter, and it decides whether a page shows cached data or
/// re-reads. Getting it wrong either way is user-visible: reload every time and
/// navigation feels laggy, never reload and a page shows another user's figures.
/// </summary>
public class SyncNotificationVersionTests
{
    [Fact]
    public void Version_AdvancesOnEveryWrite()
    {
        var before = SyncNotifications.Version;

        SyncNotifications.RaiseDataChanged();
        var afterFirst = SyncNotifications.Version;
        SyncNotifications.RaiseDataChanged();
        var afterSecond = SyncNotifications.Version;

        Assert.True(afterFirst > before);
        Assert.True(afterSecond > afterFirst);
    }

    [Fact]
    public void Version_IsUnchangedByReads()
    {
        // Nothing else touches the counter, so a page that only reads keeps
        // seeing the version it recorded and legitimately skips its reload.
        SyncNotifications.RaiseDataChanged();
        var recorded = SyncNotifications.Version;

        Assert.Equal(recorded, SyncNotifications.Version);
    }

    [Fact]
    public void SubscribersAreNotified()
    {
        var raised = 0;
        void Handler() => Interlocked.Increment(ref raised);

        SyncNotifications.Subscribe(Handler);
        try
        {
            SyncNotifications.RaiseDataChanged();
            Assert.Equal(1, raised);
        }
        finally
        {
            SyncNotifications.Unsubscribe(Handler);
        }
    }

    [Fact]
    public void AThrowingSubscriberDoesNotStopTheOthers()
    {
        var reached = false;
        void Boom() => throw new InvalidOperationException("listener blew up");
        void Fine() => reached = true;

        SyncNotifications.Subscribe(Boom);
        SyncNotifications.Subscribe(Fine);
        try
        {
            SyncNotifications.RaiseDataChanged();
        }
        finally
        {
            SyncNotifications.Unsubscribe(Boom);
            SyncNotifications.Unsubscribe(Fine);
        }

        // A write must never be blocked by a misbehaving listener - it would take
        // the save path down with it.
        Assert.True(reached);
    }

    [Fact]
    public void Version_StillAdvancesWhenAListenerThrows()
    {
        var before = SyncNotifications.Version;
        void Boom() => throw new InvalidOperationException("listener blew up");

        SyncNotifications.Subscribe(Boom);
        try
        {
            SyncNotifications.RaiseDataChanged();
        }
        finally
        {
            SyncNotifications.Unsubscribe(Boom);
        }

        Assert.True(SyncNotifications.Version > before);
    }
}