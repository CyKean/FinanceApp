namespace FinanceApp.Application.Interfaces;

/// <summary>
/// Platform session storage (remember-me credentials). Implemented per platform
/// so Infrastructure never touches platform APIs directly.
/// </summary>
public interface ISessionStore
{
    Task SaveAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<string?> LoadAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
