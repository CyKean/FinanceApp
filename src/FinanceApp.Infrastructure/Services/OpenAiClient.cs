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

    /// <summary>
    /// Response cap for chat replies. The system prompt asks for at most four
    /// short sentences, so 250 is comfortable headroom while capping the cost
    /// of a runaway reply. Free tiers are the default provider, and output
    /// tokens are billed the same as input there.
    /// </summary>
    private const int ChatMaxTokens = 250;

    public async Task<AiCompletionResult> CompleteAsync(string systemPrompt, IReadOnlyList<AiTurn> turns, CancellationToken cancellationToken = default)
        => await SendAsync(systemPrompt, turns, ChatMaxTokens, cancellationToken);

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
                var status = (int)response.StatusCode;
                _logger.LogWarning("AI request failed with {StatusCode}: {Body}", status, Truncate(errorText));

                var detail = TryGetErrorMessage(errorText);
                var failureMessage = DescribeFailure(status, detail);
                return new AiCompletionResult(false, null, failureMessage, IsConfigured: true);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseCompletion(json);
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

    /// <summary>
    /// Reads choices[0].message.content out of an OpenAI-compatible payload.
    /// <para>
    /// Every step is shape-checked: some gateways answer with a different
    /// capitalisation, a bare array, or a content array, and the previous
    /// unguarded walk turned those valid responses into "Unexpected AI error".
    /// </para>
    /// </summary>
    private static AiCompletionResult ParseCompletion(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return new AiCompletionResult(false, null, "AI returned an invalid response", IsConfigured: true);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new AiCompletionResult(false, null, "AI returned an unexpected response shape", IsConfigured: true);

            if (!TryGet(root, "choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
                return new AiCompletionResult(false, null, "AI returned an empty response", IsConfigured: true);

            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.ValueKind != JsonValueKind.Object ||
                    !TryGet(choice, "message", out var message) ||
                    message.ValueKind != JsonValueKind.Object ||
                    !TryGet(message, "content", out var content))
                {
                    continue;
                }

                var text = ReadContent(content);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // A reply cut off at max_tokens would otherwise be shown as if
                // it were complete, mid-sentence, with no indication.
                var truncated = TryGet(choice, "finish_reason", out var reason) &&
                                 reason.ValueKind == JsonValueKind.String &&
                                 reason.GetString() is "length";

                if (truncated)
                    text = text.TrimEnd() + " (reply was cut short by the model's token limit)";

                return new AiCompletionResult(true, text, null, IsConfigured: true);
            }

            return new AiCompletionResult(false, null, "AI returned an empty response", IsConfigured: true);
        }
    }

    /// <summary>Content is normally a string but some providers send parts.</summary>
    private static string? ReadContent(JsonElement content) => content.ValueKind switch
    {
        JsonValueKind.String => content.GetString(),
        JsonValueKind.Array => string.Concat(content.EnumerateArray()
            .Select(part => part.ValueKind == JsonValueKind.Object && TryGet(part, "text", out var text)
                ? text.GetString()
                : null)
            .Where(text => !string.IsNullOrEmpty(text))),
        _ => null
    };

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
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

    /// <summary>
    /// Turns an HTTP failure into something the user can act on. The previous
    /// version passed the raw provider message straight through, so an invalid
    /// key and a bad base URL both surfaced as an opaque provider sentence.
    /// </summary>
    private static string DescribeFailure(int status, string? providerDetail)
    {
        var hint = status switch
        {
            400 when LooksLikeContextLength(providerDetail) =>
                "The conversation is too long for this model. Clear the chat and try again, or pick a larger model in Settings - AI Assistant.",
            401 or 403 =>
                "Your API key was rejected. Check it in Settings - AI Assistant.",
            404 =>
                "Provider rejected the endpoint (404) - check the base URL in Settings - AI Assistant.",
            429 =>
                "Rate limit or quota reached. Wait a moment, or use a different key.",
            _ => $"AI request failed ({status})"
        };

        return string.IsNullOrWhiteSpace(providerDetail) ? hint : $"{hint}. Provider said: {providerDetail}";
    }

    private static bool LooksLikeContextLength(string? detail) =>
        detail is not null &&
        (detail.Contains("context", StringComparison.OrdinalIgnoreCase) ||
         detail.Contains("token", StringComparison.OrdinalIgnoreCase));

    private static string? TryGetErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (TryGet(root, "error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object &&
                    TryGet(error, "message", out var message) &&
                    message.ValueKind == JsonValueKind.String)
                    return message.GetString();

                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString();
            }

            if (TryGet(root, "message", out var topMessage) &&
                topMessage.ValueKind == JsonValueKind.String)
                return topMessage.GetString();
        }
        catch (JsonException)
        {
            // A non-JSON error body (an HTML proxy page, for instance) just
            // means there is no provider detail to surface.
        }

        return null;
    }

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500];
}
