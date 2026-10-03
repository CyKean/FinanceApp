namespace FinanceApp.Infrastructure.Configuration;

public class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>
    /// Groq is the default: it is OpenAI-compatible, needs no card for its
    /// free tier, and still publishes a per-model rate-limit table. Anything
    /// set in appsettings.json overrides these.
    /// </summary>
    public const string DefaultBaseUrl = "https://api.groq.com/openai/v1";

    public const string DefaultModel = "llama-3.3-70b-versatile";

    public string BaseUrl { get; set; } = DefaultBaseUrl;

    public string Model { get; set; } = DefaultModel;

    public int TimeoutSeconds { get; set; } = 30;
}