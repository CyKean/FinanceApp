namespace FinanceApp.Infrastructure.Services;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class OpenAiClient : IAiClient
{
    public const string HttpClientName = "ai";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISessionStore _sessionStore;
    private readonly IAiSettingsStore _settingsStore;
    private readonly AiOptions _options;
    private readonly ILogger<OpenAiClient> _logger;

    public OpenAiClient(
        IHttpClientFactory httpClientFactory,
        ISessionStore sessionStore,
        IAiSettingsStore settingsStore,
        IOptions<AiOptions> options,
        ILogger<OpenAiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _sessionStore = sessionStore;
        _settingsStore = settingsStore;
        _options = options.Value;
        _logger = logger;
    }

    public string DefaultBaseUrl => _options.BaseUrl;

    public string DefaultModel => _options.Model;

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var key = await _sessionStore.LoadAsync(AiClientDefaults.ApiKeySessionKey, cancellationToken);
        return !string.IsNullOrWhiteSpace(key);
    }

    public Task<AiCompletionResult> TestAsync(CancellationToken cancellationToken = default) =>
        SendAsync("Reply with exactly the word OK.", new[] { new AiTurn(ChatRoles.User, "ping") }, maxTokens: 5, cancellationToken);

    public async Task<AiCompletionResult> CompleteAsync(string systemPrompt, IReadOnlyList<AiTurn> turns, CancellationToken cancellationToken = default)
        => await SendAsync(systemPrompt, turns, maxTokens: 700, cancellationToken);

    private async Task<AiCompletionResult> SendAsync(string systemPrompt, IReadOnlyList<AiTurn> turns, int maxTokens, CancellationToken cancellationToken)
    {
        var key = await _sessionStore.LoadAsync(AiClientDefaults.ApiKeySessionKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
            return new AiCompletionResult(false, null, "No API key configured", IsConfigured: false);

        try
        {
            var settings = await GetSettingsAsync(cancellationToken);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl}/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());

            var messages = new List<object> { new { role = "system", content = systemPrompt } };
            foreach (var turn in turns)
                messages.Add(new { role = turn.Role, content = turn.Content });

            var body = new
            {
                model = settings.Model,
                messages,
                temperature = 0.7,
                max_tokens = maxTokens
            };

            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("AI request failed with {StatusCode}: {Body}", (int)response.StatusCode, Truncate(errorText));
                var detail = TryGetErrorMessage(errorText);
                var failureMessage = !string.IsNullOrWhiteSpace(detail)
                    ? detail
                    : (int)response.StatusCode == 404
                        ? "Provider rejected the endpoint (404) — check the base URL"
                        : $"AI request failed ({(int)response.StatusCode})";
                return new AiCompletionResult(false, null, failureMessage, IsConfigured: true);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                var text = content.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return new AiCompletionResult(true, text, null, IsConfigured: true);
            }

            return new AiCompletionResult(false, null, "AI returned an empty response", IsConfigured: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AiCompletionResult(false, null, "AI request timed out", IsConfigured: true);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "AI request could not reach the network");
            return new AiCompletionResult(false, null, "You appear to be offline", IsConfigured: true);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AI response could not be parsed");
            return new AiCompletionResult(false, null, "AI returned an invalid response", IsConfigured: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected AI request failure");
            return new AiCompletionResult(false, null, "Unexpected AI error", IsConfigured: true);
        }
    }

    private async Task<AiProviderSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        AiProviderSettings? settings = null;
        try
        {
            settings = await _settingsStore.GetAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading AI provider settings, using defaults");
        }

        var baseUrl = string.IsNullOrWhiteSpace(settings?.BaseUrl) ? _options.BaseUrl : settings!.BaseUrl;
        var model = string.IsNullOrWhiteSpace(settings?.Model) ? _options.Model : settings!.Model;

        return new AiProviderSettings(baseUrl.Trim().TrimEnd('/'), model.Trim());
    }

    private static string? TryGetErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty("message", out var message) &&
                    message.ValueKind == JsonValueKind.String)
                    return message.GetString();

                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString();
            }

            if (document.RootElement.TryGetProperty("message", out var topMessage) &&
                topMessage.ValueKind == JsonValueKind.String)
                return topMessage.GetString();
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500];
}
