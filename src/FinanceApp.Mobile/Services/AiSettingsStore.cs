namespace FinanceApp.Mobile.Services;

using System.Text.Json;
using FinanceApp.Application.Interfaces;
using Microsoft.Extensions.Logging;

public class AiSettingsStore : IAiSettingsStore
{
    private const string SessionKey = "financeapp.ai.settings";

    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly ISessionStore _sessionStore;
    private readonly ILogger<AiSettingsStore> _logger;

    public AiSettingsStore(ISessionStore sessionStore, ILogger<AiSettingsStore> logger)
    {
        _sessionStore = sessionStore;
        _logger = logger;
    }

    public async Task<AiProviderSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _sessionStore.LoadAsync(SessionKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<AiProviderSettings>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading AI provider settings");
            return null;
        }
    }

    public async Task SaveAsync(AiProviderSettings settings, CancellationToken cancellationToken = default)
    {
        await _sessionStore.SaveAsync(SessionKey, JsonSerializer.Serialize(settings, JsonOptions), cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _sessionStore.RemoveAsync(SessionKey, cancellationToken);
    }
}
