namespace FinanceApp.Infrastructure.Services;

using FinanceApp.Application.Interfaces;
using Microsoft.Extensions.Logging;

public class ConnectivityService : IConnectivityService
{
    private readonly ILogger<ConnectivityService> _logger;
    private static readonly HttpClient s_probeClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    // Fail closed: assume offline until a probe proves otherwise, so the
    // sync engine never burns retries against a dead network.
    private NetworkAccess _currentAccess = NetworkAccess.None;

    public NetworkAccess CurrentAccess => _currentAccess;

    public event Action<ConnectivityChangedEventArgs>? ConnectivityChanged;

    public ConnectivityService(ILogger<ConnectivityService> logger)
    {
        _logger = logger;
    }

    public async Task<NetworkAccess> CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await s_probeClient.GetAsync("https://www.google.com", cancellationToken);
            var newAccess = response.IsSuccessStatusCode ? NetworkAccess.Internet : NetworkAccess.Local;
            UpdateAccess(newAccess);
            return newAccess;
        }
        catch
        {
            UpdateAccess(NetworkAccess.None);
            return NetworkAccess.None;
        }
    }

    public void UpdateAccess(NetworkAccess newAccess)
    {
        if (_currentAccess != newAccess)
        {
            var previous = _currentAccess;
            _currentAccess = newAccess;
            _logger.LogInformation("Connectivity changed from {Previous} to {Current}", previous, newAccess);
            ConnectivityChanged?.Invoke(new ConnectivityChangedEventArgs(previous, newAccess));
        }
    }
}