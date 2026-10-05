namespace FinanceApp.Infrastructure.Supabase;

using FinanceApp.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Lazily creates and caches the single shared Supabase client.
/// Returns null when Supabase is not configured so the app keeps
/// working offline on local SQLite.
/// </summary>
public class SupabaseClientProvider
{
    /// <summary>Ceiling on the handshake so a dead network cannot stall startup.</summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(8);

    private readonly DatabaseOptions _options;
    private readonly ILogger<SupabaseClientProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private global::Supabase.Client? _client;

    public SupabaseClientProvider(IOptions<DatabaseOptions> options, ILogger<SupabaseClientProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.SupabaseUrl) &&
        !string.IsNullOrWhiteSpace(_options.SupabaseAnonKey);

    /// <summary>Base URL + anon key for direct REST calls the typed SDK does not surface.</summary>
    public string SupabaseUrl => _options.SupabaseUrl;
    public string SupabaseAnonKey => _options.SupabaseAnonKey;

    /// <summary>
    /// Returns the shared client, or null when Supabase is not configured or
    /// cannot be reached. Never throws and never blocks for long: offline-first
    /// means an unreachable backend is an expected state, not an error. Callers
    /// fall back to local SQLite.
    /// </summary>
    public async Task<global::Supabase.Client?> TryGetClientAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return null;

        if (_client != null)
            return _client;

        var connect = ConnectAsync(cancellationToken);

        // InitializeAsync takes no cancellation token, so bound the wait rather
        // than the work: a dead network must not stall app startup.
        var completed = await Task.WhenAny(connect, Task.Delay(ConnectTimeout, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        if (completed != connect)
        {
            _logger.LogWarning(
                "Supabase at {Url} did not respond within {Timeout}s. Continuing offline with local storage only.",
                _options.SupabaseUrl, ConnectTimeout.TotalSeconds);
            return null;
        }

        return await connect;
    }

    private async Task<global::Supabase.Client?> ConnectAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_client != null)
                return _client;

            var options = new global::Supabase.SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false
            };

            var client = new global::Supabase.Client(_options.SupabaseUrl, _options.SupabaseAnonKey, options);
            await client.InitializeAsync();
            _client = client;
            _logger.LogInformation("Supabase client connected to {Url}", _options.SupabaseUrl);
            return _client;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not reach Supabase at {Url}. Continuing offline with local storage only.",
                _options.SupabaseUrl);
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }
}
