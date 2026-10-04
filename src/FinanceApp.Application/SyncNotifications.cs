namespace FinanceApp.Application;

/// <summary>
/// Lightweight signal raised whenever local writes queue sync operations.
/// The background worker listens and syncs promptly instead of waiting
/// for the next interval tick. Fire-and-forget safe from any thread.
/// </summary>
public static class SyncNotifications
{
    private static event Action? DataChangedInternal;
    private static int s_version;

    /// <summary>
    /// Increments on every local write.
    /// <para>
    /// Exists so a view model can tell "the data moved under me" from "nothing
    /// happened" by comparing one integer, instead of re-querying on every page
    /// appearance or subscribing to an event it would then have to unsubscribe.
    /// </para>
    /// </summary>
    public static int Version => Volatile.Read(ref s_version);

    public static void RaiseDataChanged()
    {
        Interlocked.Increment(ref s_version);

        var handlers = DataChangedInternal;
        if (handlers is null)
            return;

        // Invoked one at a time on purpose. A multicast delegate stops at the
        // first handler that throws, so a single misbehaving listener would skip
        // every listener registered after it - and the outer catch that used to
        // swallow the exception hid exactly that.
        foreach (Action handler in handlers.GetInvocationList())
        {
            try
            {
                handler();
            }
            catch
            {
                // Never let listeners break the save path.
            }
        }
    }

    public static void Subscribe(Action handler)
    {
        DataChangedInternal += handler;
    }

    public static void Unsubscribe(Action handler)
    {
        DataChangedInternal -= handler;
    }
}
