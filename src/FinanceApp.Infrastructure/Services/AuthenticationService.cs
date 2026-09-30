namespace FinanceApp.Infrastructure.Services;

using System.Data;
using FinanceApp.Application.Interfaces;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Supabase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Session = global::Supabase.Gotrue.Session;
using SupabaseClient = global::Supabase.Client;

public class AuthenticationService : IAuthenticationService
{
    private const string UserIdKey = "financeapp.auth.userid";
    private const string EmailKey = "financeapp.auth.email";

    private static readonly string[] LocalTables =
    {
        "Accounts",
        "Categories",
        "Transactions",
        "Budgets",
        "FinancialGoals",
        "RecurringTransactions",
        "SyncOperations"
    };

    private readonly SupabaseClientProvider _clientProvider;
    private readonly ISessionStore _sessionStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);

    private Guid? _currentUserId;
    private string? _currentUserEmail;
    private bool _initialized;

    public bool RememberMe { get; set; } = true;

    public event Action<AuthStateChangedEventArgs>? AuthStateChanged;

    public AuthenticationService(
        SupabaseClientProvider clientProvider,
        ISessionStore sessionStore,
        IServiceScopeFactory scopeFactory,
        ILogger<AuthenticationService> logger)
    {
        _clientProvider = clientProvider;
        _sessionStore = sessionStore;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _initializeLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
                return;

            var client = await _clientProvider.TryGetClientAsync(cancellationToken);
            if (client != null)
            {
                client.Auth.SetPersistence(new StoreSessionPersistence(_sessionStore));
                try
                {
                    await client.Auth.LoadSessionAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not restore Supabase session");
                }

                var user = client.Auth.CurrentUser;
                if (user?.Id != null && Guid.TryParse(user.Id, out var userId) &&
                    !string.IsNullOrEmpty(client.Auth.CurrentSession?.AccessToken))
                {
                    await ReassignLocalDataAsync(userId, cancellationToken);

                    _currentUserId = userId;
                    _currentUserEmail = user.Email;

                    await _sessionStore.SaveAsync(UserIdKey, userId.ToString(), cancellationToken);
                    await _sessionStore.SaveAsync(EmailKey, user.Email ?? string.Empty, cancellationToken);

                    _logger.LogInformation("Restored Supabase session for {Email}", user.Email);
                    AuthStateChanged?.Invoke(new AuthStateChangedEventArgs(true, userId, user.Email));
                }
            }
            else
            {
                try
                {
                    var storedId = await _sessionStore.LoadAsync(UserIdKey, cancellationToken);
                    var storedEmail = await _sessionStore.LoadAsync(EmailKey, cancellationToken);
                    if (Guid.TryParse(storedId, out var userId))
                    {
                        _currentUserId = userId;
                        _currentUserEmail = storedEmail;
                        _logger.LogInformation("Restored remembered session for {Email}", storedEmail);
                        AuthStateChanged?.Invoke(new AuthStateChangedEventArgs(true, userId, storedEmail));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not restore remembered session");
                }
            }

            _initialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public async Task<AuthResultDto> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var client = await _clientProvider.TryGetClientAsync(cancellationToken);
        if (client == null)
            return await StubRegisterAsync(email, cancellationToken);

        try
        {
            ApplyPersistence(client);
            var session = await client.Auth.SignUp(email, password);
            if (session?.User == null || string.IsNullOrEmpty(session.AccessToken))
                return Failure("Account created. Check your email to confirm your address, then log in.");

            return await CompleteSignInAsync(session, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supabase registration failed");
            return Failure(FriendlyAuthError(ex));
        }
    }

    public async Task<AuthResultDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var client = await _clientProvider.TryGetClientAsync(cancellationToken);
        if (client == null)
            return await StubLoginAsync(email, cancellationToken);

        try
        {
            ApplyPersistence(client);
            var session = await client.Auth.SignInWithPassword(email, password);
            if (session?.User == null)
                return Failure("Email or password is incorrect.");

            return await CompleteSignInAsync(session, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supabase login failed");
            return Failure(FriendlyAuthError(ex));
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            var client = await _clientProvider.TryGetClientAsync(cancellationToken);
            if (client != null)
                await client.Auth.SignOut();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase sign-out failed; clearing the local session anyway");
        }

        _currentUserId = null;
        _currentUserEmail = null;

        await _sessionStore.RemoveAsync(UserIdKey, cancellationToken);
        await _sessionStore.RemoveAsync(EmailKey, cancellationToken);
        await _sessionStore.RemoveAsync(StoreSessionPersistence.SessionKey, cancellationToken);

        AuthStateChanged?.Invoke(new AuthStateChangedEventArgs(false, null, null));
    }

    public async Task<AuthResultDto> RefreshSessionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var client = await _clientProvider.TryGetClientAsync(cancellationToken);
        if (client == null)
        {
            if (_currentUserId.HasValue)
                return new AuthResultDto(true, null, _currentUserId.Value, _currentUserEmail, "stub-access-token", "stub-refresh-token");

            return new AuthResultDto(false, "No session to refresh", null, null, null, null);
        }

        if (client.Auth.CurrentUser == null)
            return new AuthResultDto(false, "No session to refresh", null, null, null, null);

        try
        {
            var session = await client.Auth.RefreshSession();
            if (session?.User?.Id == null || !Guid.TryParse(session.User.Id, out var userId))
                return new AuthResultDto(false, "Session expired. Please log in again.", null, null, null, null);

            _currentUserId = userId;
            _currentUserEmail = session.User.Email;

            if (RememberMe)
                await new StoreSessionPersistence(_sessionStore).SaveSessionAsync(session, cancellationToken);

            return new AuthResultDto(true, null, userId, session.User.Email, session.AccessToken, session.RefreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session refresh failed");
            return new AuthResultDto(false, FriendlyAuthError(ex), null, null, null, null);
        }
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _currentUserId.HasValue;
    }

    public async Task<Guid?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _currentUserId;
    }

    public async Task<string?> GetCurrentUserEmailAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _currentUserEmail;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_initialized)
            await InitializeAsync(cancellationToken);
    }

    private void ApplyPersistence(SupabaseClient client)
    {
        if (RememberMe)
            client.Auth.SetPersistence(new StoreSessionPersistence(_sessionStore));
        else
            client.Auth.SetPersistence(new NullSessionPersistence());
    }

    private async Task<AuthResultDto> CompleteSignInAsync(Session session, CancellationToken cancellationToken)
    {
        if (session.User?.Id == null || !Guid.TryParse(session.User.Id, out var userId))
            return Failure("Sign-in failed. Please try again.");

        var email = session.User.Email;

        await ReassignLocalDataAsync(userId, cancellationToken);

        _currentUserId = userId;
        _currentUserEmail = email;

        if (RememberMe)
        {
            await new StoreSessionPersistence(_sessionStore).SaveSessionAsync(session, cancellationToken);
            await _sessionStore.SaveAsync(UserIdKey, userId.ToString(), cancellationToken);
            await _sessionStore.SaveAsync(EmailKey, email ?? string.Empty, cancellationToken);
        }
        else
        {
            await _sessionStore.RemoveAsync(StoreSessionPersistence.SessionKey, cancellationToken);
            await _sessionStore.RemoveAsync(UserIdKey, cancellationToken);
            await _sessionStore.RemoveAsync(EmailKey, cancellationToken);
        }

        _logger.LogInformation("Signed in {Email} as {UserId}", email, userId);
        AuthStateChanged?.Invoke(new AuthStateChangedEventArgs(true, userId, email));

        return new AuthResultDto(true, null, userId, email, session.AccessToken, session.RefreshToken);
    }

    private async Task ReassignLocalDataAsync(Guid authenticatedUserId, CancellationToken cancellationToken)
    {
        try
        {
            var storedId = await _sessionStore.LoadAsync(UserIdKey, cancellationToken);
            if (!Guid.TryParse(storedId, out var previousUserId) || previousUserId == authenticatedUserId)
                return;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FinanceAppDbContext>();
            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            var from = previousUserId.ToString().ToUpperInvariant();
            var to = authenticatedUserId.ToString().ToUpperInvariant();

            foreach (var table in LocalTables)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"UPDATE {table} SET UserId = @to WHERE UPPER(UserId) = @from";

                var fromParameter = command.CreateParameter();
                fromParameter.ParameterName = "@from";
                fromParameter.Value = from;
                command.Parameters.Add(fromParameter);

                var toParameter = command.CreateParameter();
                toParameter.ParameterName = "@to";
                toParameter.Value = to;
                command.Parameters.Add(toParameter);

                var affected = await command.ExecuteNonQueryAsync(cancellationToken);
                if (affected > 0)
                {
                    _logger.LogInformation(
                        "Reassigned {Count} {Table} rows from local user {Previous} to authenticated user {Authenticated}",
                        affected, table, previousUserId, authenticatedUserId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not reassign local data to authenticated user {UserId}", authenticatedUserId);
        }
    }

    private async Task<AuthResultDto> StubRegisterAsync(string email, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Register user {Email} - stub implementation", email);

        var userId = Guid.NewGuid();
        _currentUserId = userId;
        _currentUserEmail = email;

        await PersistStubSessionAsync(userId, email, cancellationToken);
        AuthStateChanged?.Invoke(new AuthStateChangedEventArgs(true, userId, email));

        return new AuthResultDto(true, null, userId, email, "stub-access-token", "stub-refresh-token");
    }

    private async Task<AuthResultDto> StubLoginAsync(string email, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Login user {Email} - stub implementation", email);

        var userId = Guid.NewGuid();
        _currentUserId = userId;
        _currentUserEmail = email;

        await PersistStubSessionAsync(userId, email, cancellationToken);
        AuthStateChanged?.Invoke(new AuthStateChangedEventArgs(true, userId, email));

        return new AuthResultDto(true, null, userId, email, "stub-access-token", "stub-refresh-token");
    }

    private async Task PersistStubSessionAsync(Guid userId, string email, CancellationToken cancellationToken)
    {
        if (!RememberMe)
            return;

        await _sessionStore.SaveAsync(UserIdKey, userId.ToString(), cancellationToken);
        await _sessionStore.SaveAsync(EmailKey, email, cancellationToken);
    }

    private static AuthResultDto Failure(string message) => new(false, message, null, null, null, null);

    private static string FriendlyAuthError(Exception exception)
    {
        var message = exception.Message ?? string.Empty;

        if (message.Contains("Invalid login credentials", StringComparison.OrdinalIgnoreCase))
            return "Email or password is incorrect.";
        if (message.Contains("User already registered", StringComparison.OrdinalIgnoreCase))
            return "An account with this email already exists. Tap Log in instead.";
        if (message.Contains("Email not confirmed", StringComparison.OrdinalIgnoreCase))
            return "Confirm your email first - check your inbox for the confirmation link.";
        if (message.Contains("Password should be", StringComparison.OrdinalIgnoreCase))
            return "Password must be at least 6 characters.";
        if (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("429", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase))
            return "Too many attempts. Please wait a minute and try again.";
        if (exception is HttpRequestException or TaskCanceledException)
            return "Couldn't reach the server. Check your connection and try again.";

        return "Something went wrong. Please try again.";
    }
}
