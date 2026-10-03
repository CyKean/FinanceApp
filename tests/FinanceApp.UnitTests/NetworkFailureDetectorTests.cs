namespace FinanceApp.UnitTests;

using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using FinanceApp.Infrastructure.Services;
using Xunit;

/// <summary>
/// Signing in offline once reported "Something went wrong. Please try again."
/// because GoTrue wraps transport failures in <c>GotrueException("Connection
/// failure")</c> and nothing matched on it. These tests pin the classification.
/// </summary>
public class NetworkFailureDetectorTests
{
    [Fact]
    public void RecognisesTheGotrueWrapperAroundADnsFailure()
    {
        // The shape from the Android log: GotrueException -> HttpRequestException
        // -> UnknownHostException. Only the Java one carries the useful text, and
        // its managed type is opaque.
        var inner = new HttpRequestException("Connection failure");
        var exception = new InvalidOperationException(
            "Connection failure",
            new InvalidOperationException("Unable to resolve host \"eemzsuvgloguilefijvm.supabase.co\": No address associated with hostname", inner));

        Assert.True(NetworkFailureDetector.IsNetworkFailure(exception));
    }

    [Fact]
    public void RecognisesTheEaiNoDataMessage()
    {
        var exception = new Exception("android_getaddrinfo failed: EAI_NODATA (No address associated with hostname)");

        Assert.True(NetworkFailureDetector.IsNetworkFailure(exception));
    }

    [Fact]
    public void RecognisesCommonTransportTypes()
    {
        Assert.True(NetworkFailureDetector.IsNetworkFailure(new HttpRequestException()));
        Assert.True(NetworkFailureDetector.IsNetworkFailure(new SocketException()));
        Assert.True(NetworkFailureDetector.IsNetworkFailure(new TimeoutException()));
        Assert.True(NetworkFailureDetector.IsNetworkFailure(new TaskCanceledException()));
    }

    [Fact]
    public void RecognisesATransportTypeNestedDeepInTheChain()
    {
        var exception = new Exception("outer", new Exception("middle", new SocketException(110)));

        Assert.True(NetworkFailureDetector.IsNetworkFailure(exception));
    }

    [Theory]
    [InlineData("Invalid login credentials")]
    [InlineData("User already registered")]
    [InlineData("Email not confirmed")]
    [InlineData("Password should be at least 6 characters.")]
    [InlineData("Too Many Requests")]
    public void DoesNotMistakeARejectedCredentialForANetworkProblem(string serverMessage)
    {
        // These are HTTP 4xx answers: the network worked. Reporting them as a
        // connection problem would send users off to check their Wi-Fi.
        Assert.False(NetworkFailureDetector.IsNetworkFailure(new Exception(serverMessage)));
    }

    [Fact]
    public void ReturnsFalseForNull()
    {
        Assert.False(NetworkFailureDetector.IsNetworkFailure(null));
    }
}