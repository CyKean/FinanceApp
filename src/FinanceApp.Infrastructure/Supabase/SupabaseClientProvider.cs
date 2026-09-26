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

    /// <summary>
    /// Returns the shared client, or null when Supabase is not configured.
    /// Throws only for genuine connection failures.
    /// </summary>
    public async Task<global::Supabase.Client?> TryGetClientAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return null;

        if (_client != null)
            return _client;

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
        finally
        {
            _lock.Release();
        }
    }
}
