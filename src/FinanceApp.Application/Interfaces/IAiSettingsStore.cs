namespace FinanceApp.Application.Interfaces;

public record AiProviderSettings(string BaseUrl, string Model);

public interface IAiSettingsStore
{
    Task<AiProviderSettings?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AiProviderSettings settings, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
