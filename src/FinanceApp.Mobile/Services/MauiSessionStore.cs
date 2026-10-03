namespace FinanceApp.Mobile.Services;

using FinanceApp.Application.Interfaces;

/// <summary>
/// Session storage backed by encrypted SecureStorage with a
/// Preferences fallback for devices without a lock screen.
/// </summary>
public class MauiSessionStore : ISessionStore
{
    public async Task SaveAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            await SecureStorage.SetAsync(key, value);

            // SecureStorage can start working after a failed write earlier (the
            // user sets a screen lock, say). Leaving the plaintext Preferences
            // copy behind means LoadAsync keeps finding the unencrypted value,
            // so a "downgraded" key would stay readable indefinitely.
            if (Preferences.Get(key, null) is not null)
                Preferences.Remove(key);
        }
        catch
        {
            // No lock screen or keystore unavailable: keep it, but unencrypted.
            Preferences.Set(key, value);
        }
    }

    public async Task<string?> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await SecureStorage.GetAsync(key);
            if (!string.IsNullOrEmpty(value))
                return value;
        }
        catch
        {
            // Fall through to Preferences.
        }

        var fallback = Preferences.Get(key, string.Empty);
        return string.IsNullOrEmpty(fallback) ? null : fallback;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            SecureStorage.Remove(key);
        }
        catch
        {
            // Best effort - Preferences fallback below still clears.
        }

        Preferences.Remove(key);
        return Task.CompletedTask;
    }
}
