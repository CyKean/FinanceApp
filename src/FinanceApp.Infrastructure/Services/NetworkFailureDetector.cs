namespace FinanceApp.Infrastructure.Services;

using System.Net.Sockets;

/// <summary>
/// Tells a broken network apart from a server that said no.
/// <para>
/// Supabase wraps every transport failure in a
/// <c>GotrueException("Connection failure")</c>, so a type check on the outermost
/// exception reports a dropped connection as an application error. That is what
/// made signing in offline show "Something went wrong. Please try again."
/// </para>
/// </summary>
public static class NetworkFailureDetector
{
    /// <summary>
    /// Substrings for failures the runtime reports only as text. Android raises
    /// DNS resolution errors as a Java exception type that is not visible to
    /// managed code, so the message is all there is to go on.
    /// </summary>
    private static readonly string[] Markers =
    {
        "Connection failure",
        "Unable to resolve host",
        "No address associated with hostname",
        "Name or service not known",
        "nodename nor servname",
        "Connection refused",
        "Connection reset",
        "Network is unreachable",
        "No such host is known",
        "failed to resolve host",
        "failed to connect"
    };

    public static bool IsNetworkFailure(Exception? exception)
    {
        // The exception chain matters: the useful type or message is usually on an
        // inner exception, wrapped by whichever client library was in play.
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException or SocketException or TimeoutException or TaskCanceledException or OperationCanceledException)
                return true;

            var message = current.Message;
            if (string.IsNullOrEmpty(message))
                continue;

            foreach (var marker in Markers)
            {
                if (message.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}