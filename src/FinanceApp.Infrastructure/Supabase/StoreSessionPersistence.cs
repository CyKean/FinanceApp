namespace FinanceApp.Infrastructure.Supabase;

using System.Text.Json;
using FinanceApp.Application.Interfaces;
using global::Supabase.Gotrue.Interfaces;
using Session = global::Supabase.Gotrue.Session;

public class StoreSessionPersistence : IGotrueSessionPersistence<Session>
{
    public const string SessionKey = "financeapp.auth.session";

    private readonly ISessionStore _sessionStore;

    public StoreSessionPersistence(ISessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }

    public void SaveSession(Session session) =>
        Task.Run(() => SaveSessionAsync(session, CancellationToken.None)).GetAwaiter().GetResult();

    public Session? LoadSession() =>
        Task.Run(() => LoadSessionAsync(CancellationToken.None)).GetAwaiter().GetResult();

    public void DestroySession() =>
        Task.Run(() => DestroySessionAsync(CancellationToken.None)).GetAwaiter().GetResult();

    public async Task SaveSessionAsync(Session session, CancellationToken cancellationToken = default)
    {
        try
        {
            await _sessionStore.SaveAsync(SessionKey, JsonSerializer.Serialize(session), cancellationToken);
        }
        catch
        {
        }
    }

    public async Task<Session?> LoadSessionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _sessionStore.LoadAsync(SessionKey, cancellationToken);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<Session>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task DestroySessionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _sessionStore.RemoveAsync(SessionKey, cancellationToken);
        }
        catch
        {
        }
    }
}

public class NullSessionPersistence : IGotrueSessionPersistence<Session>
{
    public void SaveSession(Session session)
    {
    }

    public Session? LoadSession() => null;

    public void DestroySession()
    {
    }

    public Task SaveSessionAsync(Session session, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<Session?> LoadSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult<Session?>(null);

    public Task DestroySessionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
