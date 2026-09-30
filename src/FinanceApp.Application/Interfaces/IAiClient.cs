namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.DTOs;

public interface IAiClient
{
    string DefaultBaseUrl { get; }

    string DefaultModel { get; }

    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    Task<AiCompletionResult> TestAsync(CancellationToken cancellationToken = default);

    Task<AiCompletionResult> CompleteAsync(string systemPrompt, IReadOnlyList<AiTurn> turns, CancellationToken cancellationToken = default);
}

public static class AiClientDefaults
{
    public const string ApiKeySessionKey = "financeapp.ai.apikey";
}
