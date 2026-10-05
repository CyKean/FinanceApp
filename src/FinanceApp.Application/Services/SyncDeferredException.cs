namespace FinanceApp.Application.Services;

/// <summary>
/// Thrown when a push or pull could not be attempted at all, because the Supabase
/// side was not ready to receive it - no configured project, no reachable network,
/// or no signed-in session to send.
/// <para>
/// This is deliberately not an error. Nothing was rejected, so the operation is
/// still owed and must stay pending rather than being marked synced or counted
/// against the retry budget: both of those are permanent, and neither is true of
/// a push that was never sent. The distinction matters because the alternative -
/// carrying on as if the write had happened - loses the change outright.
/// </para>
/// </summary>
public sealed class SyncDeferredException : Exception
{
    public SyncDeferredException(string message) : base(message) { }
    public SyncDeferredException(string message, Exception innerException) : base(message, innerException) { }
}