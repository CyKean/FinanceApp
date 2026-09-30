namespace FinanceApp.Application;

/// <summary>
/// Lightweight signal raised whenever local writes queue sync operations.
/// The background worker listens and syncs promptly instead of waiting
/// for the next interval tick. Fire-and-forget safe from any thread.
/// </summary>
public static class SyncNotifications
{
    private static event Action? DataChangedInternal;

    public static void RaiseDataChanged()
    {
        try
        {
            DataChangedInternal?.Invoke();
        }
        catch
        {
            // Never let listeners break the save path.
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
