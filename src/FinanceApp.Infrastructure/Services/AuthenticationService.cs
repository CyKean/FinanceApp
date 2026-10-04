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

/// <summary>
/// Offline-first authentication.
///
/// Credentials live in the device-local <c>LocalUsers</c> table, so register
/// and sign-in always resolve without a network. Supabase is a best-effort
/// second factor: it mirrors the account and holds the session that sync uses,
/// and every call into it is non-blocking for the sign-in path and non-fatal
/// when it fails. A user who never connects still gets a working app.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private const string UserIdKey = "financeapp.auth.userid";
    private const string EmailKey = "financeapp.auth.email";

    private const int MinimumPasswordLength = 6;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(30);

    /// <summary>Local sessions are not token-backed, so these are inert placeholders.</summary>
    private const string LocalAccessToken = "local-session";
    private const string LocalRefreshToken = "local-session-refresh";

    /// <summary>Upper bound on any cloud round-trip made during a foreground auth call.</summary>
    private static readonly TimeSpan CloudTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Tighter budget for "is this email on another device?". It runs on the UI
    /// thread and the answer it can give is best-effort, so it should not hold
    /// the login screen when the network turns out to be dead.
    /// </summary>
    private static readonly TimeSpan CloudProbeTimeout = TimeSpan.FromSeconds(6);

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
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private readonly Dictionary<string, AttemptState> _failedAttempts = new(StringComparer.Ordinal);
    private readonly object _failedAttemptsLock = new();

    private Guid? _currentUserId;
    private string? _currentUserEmail;
    private bool _initialized;

    public bool RememberMe { get; set; } = true;

    public event Action<AuthStateChangedEventArgs>? AuthStateChanged;

    public AuthenticationService(
        SupabaseClientProvider clientProvider,
        ISessionStore sessionStore,
        IServiceScopeFactory scopeFactory,
        IPasswordHasher passwordHasher,
        ILogger<AuthenticationService> logger)
    {
        _clientProvider = clientProvider;
        _sessionStore = sessionStore;
        _scopeFactory = scopeFactory;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _initializeLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
                return;

            await RestoreLocalSessionAsync(cancellationToken);

            // Detached on purpose: this runs from a hosted service at startup and
            // would otherwise hold app launch for the length of a Supabase
            // handshake. The local session above is already authoritative, so the
            // cloud pass only ever refines it. CancellationToken.None because the
            // startup token is tied to host shutdown, not to this work.
            _ = RestoreCloudSessionAsync(CancellationToken.None);

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

        if (ValidateCredentials(email, password, requireMinimumLength: true) is { } invalid)
            return Failure(invalid);

        var normalized = LocalAccountStore.Normalize(email);

        LocalAccountDto account;
        try
        {
            if (await WithStoreAsync(store => store.FindByEmailAsync(normalized, cancellationToken)) is not null)
                return Failure("An account with this email already exists. Tap Log in instead.");

            account = await WithStoreAsync(store => store.CreateAsync(normalized, password, cancellationToken));
        }
        catch (DbUpdateException ex)
        {
            // The unique index on Email is the authority; the pre-check above is
            // only a fast path and can lose a race with another registration.
            _logger.LogWarning(ex, "Could not create local account for {Email}", normalized);
            return Failure("An account with this email already exists. Tap Log in instead.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Could not create local account for {Email}", normalized);
            return Failure("An account with this email already exists. Tap Log in instead.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed for {Email}", normalized);
            return Failure("Could not create your account. Please try again.");
        }

        ClearFailedAttempts(normalized);
        _logger.LogInformation("Registered {Email} locally as {UserId}", normalized, account.Id);

        var result = await CompleteLocalSignInAsync(account, cancellationToken);

        // Mirror to Supabase so the account can sync. Detached on purpose: the
        // account is already usable, so this must not delay or fail sign-up.
        _ = ProvisionCloudAccountAsync(normalized, password, account.Id);

        return result;
    }

    public async Task<AuthResultDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (ValidateCredentials(email, password, requireMinimumLength: false) is { } invalid)
            return Failure(invalid);

        var normalized = LocalAccountStore.Normalize(email);

        if (GetLockoutMessage(normalized) is { } lockedOut)
            return Failure(lockedOut);

        var account = await WithStoreAsync(store => store.FindByEmailAsync(normalized, cancellationToken));

        if (account is null)
            return await LoginUnknownLocalAccountAsync(normalized, password, cancellationToken);

        if (!_passwordHasher.Verify(password, account.PasswordHash))
        {
            RegisterFailedAttempt(normalized);
            return Failure("Email or password is incorrect.");
        }

        ClearFailedAttempts(normalized);
        await WithStoreAsync(store => store.RecordSignInAsync(account.Id, cancellationToken));

        var result = await CompleteLocalSignInAsync(account, cancellationToken);

        // Detached: a known-good local sign-in must never wait on the network.
        // This is what lets a locally created account pick up a cloud session.
        _ = AttachCloudSessionAsync(normalized, password, account.Id);

        return result;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var email = _currentUserEmail;

        _currentUserId = null;
        _currentUserEmail = null;

        await ClearStoredSessionAsync(cancellationToken);
        AuthStateChanged?.Invoke(new AuthStateChangedEventArgs(false, null, null));

        // The shell is a singleton, so pages - and the data cached on their view
        // models - outlive the session. Without this the next sign-in could be
        // short-circuited as "still current" and the previous user's figures would
        // be sitting on screen.
        FinanceApp.Application.SyncNotifications.RaiseDataChanged();

        _logger.LogInformation("Signed out {Email}", email);

        // Detached so signing out is instant offline. Clearing the stored session
        // above is what actually ends the session; the cloud call just revokes
        // the access token server-side.
        _ = SignOutCloudAsync();
    }

    private async Task SignOutCloudAsync()
    {
        try
        {
            var client = await TryGetClientAsync(CancellationToken.None);
            if (client is not null)
                await client.Auth.SignOut();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cloud sign-out failed; the local session is already cleared");
        }
    }

    public async Task<AuthResultDto> RefreshSessionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (!_currentUserId.HasValue)
            return Failure("No session to refresh");

        // Local sessions do not expire, so this succeeds regardless of the
        // network. Refreshing the cloud copy is a bonus for sync.
        await RefreshCloudSessionAsync(cancellationToken);

        return new AuthResultDto(true, null, _currentUserId.Value, _currentUserEmail, LocalAccessToken, LocalRefreshToken);
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

    private async Task RestoreLocalSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var storedId = await _sessionStore.LoadAsync(UserIdKey, cancellationToken);
            if (!Guid.TryParse(storedId, out var userId))
                return;

            _currentUserId = userId;
            _currentUserEmail = await _sessionStore.LoadAsync(EmailKey, cancellationToken);

            _logger.LogInformation("Restored local session for {Email}", _currentUserEmail);
            AuthStateChanged?.Invoke(new AuthStateChangedEventArgs(true, userId, _currentUserEmail));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not restore the remembered session");
        }
    }

    private async Task RestoreCloudSessionAsync(CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
            return;

        // Runs detached from InitializeAsync, so nothing may escape.
        try
        {
            var client = await TryGetClientAsync(cancellationToken);
            if (client is null)
                return;

            client.Auth.SetPersistence(new StoreSessionPersistence(_sessionStore));
            await client.Auth.LoadSessionAsync(cancellationToken);

            var user = client.Auth.CurrentUser;
            if (user?.Id == null ||
                !Guid.TryParse(user.Id, out var cloudId) ||
                string.IsNullOrEmpty(client.Auth.CurrentSession?.AccessToken))
            {
                return;
            }

            if (_currentUserId == cloudId)
            {
                _currentUserEmail ??= user.Email;
                return;
            }

            await AdoptCloudIdentityAsync(_currentUserId, cloudId, user.Email, cancellationToken);
            _logger.LogInformation("Adopted cloud session for {Email} as {UserId}", user.Email, cloudId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not restore the cloud session; staying on the local session");
        }
    }

    /// <summary>
    /// Local sign-in. Any rows still stamped with a previously signed-in user are
    /// moved onto this identity so a fresh install's demo data follows the account.
    /// </summary>
    private async Task<AuthResultDto> CompleteLocalSignInAsync(LocalAccountDto account, CancellationToken cancellationToken)
    {
        await ReassignLocalDataAsync(_currentUserId, account.Id, cancellationToken);

        _currentUserId = account.Id;
        _currentUserEmail = account.Email;

        if (RememberMe)
        {
            await _sessionStore.SaveAsync(UserIdKey, account.Id.ToString(), cancellationToken);
            await _sessionStore.SaveAsync(EmailKey, account.Email, cancellationToken);
        }
        else
        {
            await ClearStoredSessionAsync(cancellationToken);
        }

        _logger.LogInformation("Signed in {Email} as {UserId}", account.Email, account.Id);
        AuthStateChanged?.Invoke(new AuthStateChangedEventArgs(true, account.Id, account.Email));

        return new AuthResultDto(true, null, account.Id, account.Email, LocalAccessToken, LocalRefreshToken);
    }

    private void CompleteCloudSignIn(Guid userId, string? email)
    {
        _currentUserId = userId;
        _currentUserEmail = email;

        AuthStateChanged?.Invoke(new AuthStateChangedEventArgs(true, userId, email));
    }

    private async Task PersistSessionAsync(Guid userId, string? email, CancellationToken cancellationToken)
    {
        await _sessionStore.SaveAsync(UserIdKey, userId.ToString(), cancellationToken);
        await _sessionStore.SaveAsync(EmailKey, email ?? string.Empty, cancellationToken);
    }

    private async Task ClearStoredSessionAsync(CancellationToken cancellationToken)
    {
        await _sessionStore.RemoveAsync(UserIdKey, cancellationToken);
        await _sessionStore.RemoveAsync(EmailKey, cancellationToken);
        await _sessionStore.RemoveAsync(StoreSessionPersistence.SessionKey, cancellationToken);
    }

    /// <summary>
    /// No local account for this email. The only way to authenticate is against
    /// Supabase, so this is the one path that genuinely needs the network - and
    /// only for accounts created on another device. A verified session is cached
    /// as a local account, so the next sign-in works offline.
    /// </summary>
    private async Task<AuthResultDto> LoginUnknownLocalAccountAsync(string normalizedEmail, string password, CancellationToken cancellationToken)
    {
        // The row count is the difference between "you typed the wrong address"
        // and "this install has no accounts at all" (fresh install, cleared app
        // data, or an account created by a build without local auth). Cheap, and
        // it is the first thing worth knowing when a user reports being locked out.
        var localAccounts = await WithStoreAsync(store => store.CountAsync(cancellationToken));
        _logger.LogWarning(
            "No local account for {Email} - this device holds {Count} account(s). Falling back to the cloud.",
            normalizedEmail, localAccounts);

        if (!_clientProvider.IsConfigured)
            return Failure(NoAccountMessage(normalizedEmail, canReachCloud: false));

        Session? session;
        try
        {
            // Shorter than the general cloud budget: this runs on the UI thread and
            // the only thing it can achieve is finding an account from another device.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CloudProbeTimeout);

            var client = await _clientProvider.TryGetClientAsync(timeout.Token);
            if (client is null)
                return Failure(NoAccountMessage(normalizedEmail, canReachCloud: false));

            ApplyPersistence(client);
            session = await client.Auth.SignInWithPassword(normalizedEmail, password);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Timed out verifying {Email} against the cloud", normalizedEmail);
            return Failure(NoAccountMessage(normalizedEmail, canReachCloud: false));
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            // Offline (or the backend is unreachable): this device is the only
            // place the account could be, and it is not here. Say so plainly
            // instead of surfacing a generic failure.
            _logger.LogInformation("Cannot reach the cloud to check {Email}: {Reason}", normalizedEmail, FriendlyAuthError(ex));
            return Failure(NoAccountMessage(normalizedEmail, canReachCloud: false));
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Cloud sign-in attempt failed for {Email}: {Reason}", normalizedEmail, FriendlyAuthError(ex));
            return Failure(FriendlyAuthError(ex));
        }

        if (!TryGetCloudId(session, out var cloudId))
            return Failure(NoAccountMessage(normalizedEmail, canReachCloud: true));

        var email = session!.User?.Email ?? normalizedEmail;

        try
        {
            // Key the cached account on the cloud uuid so the id already matches
            // auth.uid() and RLS accepts synced rows - no identity migration.
            var account = await WithStoreAsync(store => store.CreateAsync(email, password, cloudId, cancellationToken));
            await WithStoreAsync(store => store.RecordSignInAsync(account.Id, cancellationToken));

            var result = await CompleteLocalSignInAsync(account, cancellationToken);
            _logger.LogInformation("Cached cloud account {Email} locally for offline sign-in", email);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Verified {Email} against the cloud but could not cache it locally", email);
            CompleteCloudSignIn(cloudId, email);
            return new AuthResultDto(true, null, cloudId, email, session.AccessToken, session.RefreshToken);
        }
    }

    /// <summary>
    /// Creates the matching Supabase user so the account can sync. Fire-and-forget:
    /// offline registration stays instant and never depends on this succeeding.
    /// <para>
    /// With email confirmation disabled on the Supabase project (see
    /// scripts/configure-supabase-auth.ps1) the mirror comes back usable, so the
    /// cloud uuid is adopted right here and sync starts pushing on first sign-in
    /// instead of waiting for the next sign-in.
    /// </para>
    /// </summary>
    private async Task ProvisionCloudAccountAsync(string normalizedEmail, string password, Guid localUserId)
    {
        if (!_clientProvider.IsConfigured)
            return;

        try
        {
            using var timeout = new CancellationTokenSource(CloudTimeout);
            var client = await _clientProvider.TryGetClientAsync(timeout.Token);
            if (client is null)
                return;

            ApplyPersistence(client);
            var session = await client.Auth.SignUp(normalizedEmail, password);

            if (TryGetCloudId(session, out var cloudId) && !string.IsNullOrEmpty(session!.AccessToken))
            {
                await AdoptCloudIdentityAsync(localUserId, cloudId, normalizedEmail, timeout.Token);
                _logger.LogInformation("Mirrored {Email} to the cloud as {CloudId}; sync is live", normalizedEmail, cloudId);
                return;
            }

            // Confirmation is still switched on for this project, so the mirror
            // exists but cannot be used until the address is confirmed. The user
            // is already signed in locally, so this is not surfaced.
            _logger.LogInformation(
                "Cloud mirror for {Email} needs email confirmation before it can sync. Local data is unaffected.",
                normalizedEmail);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Could not mirror {Email} to the cloud: {Reason}", normalizedEmail, FriendlyAuthError(ex));

            // Most often the address already exists server-side (registered on
            // another device). Signing in is what gives this device a session.
            await AttachCloudSessionAsync(normalizedEmail, password, localUserId);
        }
    }

    /// <summary>
    /// Signs in to the cloud for an account that already signed in locally, so
    /// sync gets a token. Runs detached; the local session is already valid and
    /// stays that way whether this succeeds or not.
    /// </summary>
    private async Task AttachCloudSessionAsync(string normalizedEmail, string password, Guid localUserId)
    {
        if (!_clientProvider.IsConfigured)
            return;

        try
        {
            using var timeout = new CancellationTokenSource(CloudTimeout);
            var client = await _clientProvider.TryGetClientAsync(timeout.Token);
            if (client is null)
                return;

            ApplyPersistence(client);
            var session = await client.Auth.SignInWithPassword(normalizedEmail, password);
            if (!TryGetCloudId(session, out var cloudId))
                return;

            await AdoptCloudIdentityAsync(localUserId, cloudId, session!.User?.Email ?? normalizedEmail, timeout.Token);
            _logger.LogInformation("Attached cloud session for {Email} as {CloudId}", normalizedEmail, cloudId);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "No cloud session for {Email}: {Reason}. Sync stays offline.", normalizedEmail, FriendlyAuthError(ex));
        }
    }

    private async Task RefreshCloudSessionAsync(CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
            return;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CloudTimeout);

            var client = await _clientProvider.TryGetClientAsync(timeout.Token);
            if (client is null || client.Auth.CurrentUser == null)
                return;

            var session = await client.Auth.RefreshSession();
            if (!TryGetCloudId(session, out var cloudId))
                return;

            await AdoptCloudIdentityAsync(_currentUserId, cloudId, session!.User?.Email, timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cloud session refresh failed; the local session stays valid");
        }
    }

    /// <summary>
    /// Remote rows are keyed by <c>auth.uid()</c>, so the local id must become the
    /// cloud id or every push is rejected by RLS. The account row is moved first:
    /// sign-in is always driven by the account's current id, so an interrupted
    /// adoption finishes on the next launch.
    /// </summary>
    private async Task AdoptCloudIdentityAsync(Guid? previousUserId, Guid cloudId, string? email, CancellationToken cancellationToken)
    {
        if (previousUserId == cloudId)
        {
            _currentUserEmail ??= email;
            return;
        }

        if (previousUserId.HasValue && previousUserId.Value != Guid.Empty)
        {
            await WithStoreAsync(store => store.ReassignIdAsync(previousUserId.Value, cloudId, cancellationToken));
            await ReassignLocalDataAsync(previousUserId, cloudId, cancellationToken);
        }

        CompleteCloudSignIn(cloudId, email);

        if (RememberMe)
            await PersistSessionAsync(cloudId, email, cancellationToken);
    }

    private async Task ReassignLocalDataAsync(Guid? previousUserId, Guid newUserId, CancellationToken cancellationToken)
    {
        if (!previousUserId.HasValue || previousUserId.Value == Guid.Empty || previousUserId.Value == newUserId)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FinanceAppDbContext>();
            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            var from = previousUserId.Value.ToString().ToUpperInvariant();
            var to = newUserId.ToString().ToUpperInvariant();

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
                        "Reassigned {Count} {Table} rows from local user {Previous} to {Current}",
                        affected, table, previousUserId.Value, newUserId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not reassign local data from {Previous} to {Current}", previousUserId, newUserId);
        }
    }

    private async Task<SupabaseClient?> TryGetClientAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CloudTimeout);
            return await _clientProvider.TryGetClientAsync(timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase client is unavailable; continuing offline");
            return null;
        }
    }

    private void ApplyPersistence(SupabaseClient client)
    {
        client.Auth.SetPersistence(RememberMe
            ? new StoreSessionPersistence(_sessionStore)
            : new NullSessionPersistence());
    }

    private async Task<T> WithStoreAsync<T>(Func<ILocalAccountStore, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ILocalAccountStore>();
        return await action(store);
    }

    private async Task WithStoreAsync(Func<ILocalAccountStore, Task> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ILocalAccountStore>();
        await action(store);
    }

    /// <summary>
    /// GoTrue hands back uuids as strings; everything downstream keys off a Guid.
    /// </summary>
    private static bool TryGetCloudId(Session? session, out Guid cloudId)
    {
        cloudId = Guid.Empty;

        if (session?.User?.Id is not { Length: > 0 } id || !Guid.TryParse(id, out cloudId))
        {
            cloudId = Guid.Empty;
            return false;
        }

        return true;
    }

    private static string? ValidateCredentials(string? email, string? password, bool requireMinimumLength)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "Email is required";

        if (!IsValidEmail(email))
            return "Enter a valid email address";

        if (string.IsNullOrEmpty(password))
            return "Password is required";

        if (requireMinimumLength && password.Length < MinimumPasswordLength)
            return $"Password must be at least {MinimumPasswordLength} characters";

        return null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var trimmed = email.Trim();
            var address = new System.Net.Mail.MailAddress(trimmed);
            return address.Address == trimmed &&
                   address.Host.Contains('.') &&
                   !address.Host.EndsWith('.');
        }
        catch
        {
            return false;
        }
    }

    private static string NoAccountMessage(string normalizedEmail) =>
        $"No account for {normalizedEmail} on this device. Check the address, or create an account to get started offline.";

    private string? GetLockoutMessage(string normalizedEmail)
    {
        lock (_failedAttemptsLock)
        {
            if (!_failedAttempts.TryGetValue(normalizedEmail, out var state))
                return null;

            var remaining = state.LockedUntil - DateTime.UtcNow;
            return remaining > TimeSpan.Zero
                ? $"Too many failed attempts. Try again in {Math.Ceiling(remaining.TotalSeconds):0} seconds."
                : null;
        }
    }

    private void RegisterFailedAttempt(string normalizedEmail)
    {
        lock (_failedAttemptsLock)
        {
            var state = _failedAttempts.GetValueOrDefault(normalizedEmail);
            var failures = state.Failures + 1;

            _failedAttempts[normalizedEmail] = failures >= MaxFailedAttempts
                ? new AttemptState(failures, DateTime.UtcNow.Add(LockoutDuration))
                : new AttemptState(failures, default);

            if (failures >= MaxFailedAttempts)
                _logger.LogWarning("Locking sign-in for {Email} after {Failures} failed attempts", normalizedEmail, failures);
        }
    }

    private void ClearFailedAttempts(string normalizedEmail)
    {
        lock (_failedAttemptsLock)
        {
            _failedAttempts.Remove(normalizedEmail);
        }
    }

    /// <summary>
    /// True when the failure was the network rather than the server. GoTrue wraps
    /// every transport error in a <c>GotrueException("Connection failure")</c>, so
    /// matching on the exception type alone reports a dropped connection as
    /// "something went wrong" - which is what a user sees when signing in offline.
    /// </summary>
    private static bool IsTransportFailure(Exception exception) => NetworkFailureDetector.IsNetworkFailure(exception);

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
            return $"Password must be at least {MinimumPasswordLength} characters.";
        if (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("429", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase))
            return "Too many attempts. Please wait a minute and try again.";

        if (IsTransportFailure(exception))
            return "Couldn't reach the server. Check your connection and try again.";

        return "Something went wrong. Please try again.";
    }

    /// <summary>
    /// The email is unknown to this device, which is only a dead end if the cloud
    /// cannot be asked either. Say which of the two happened, because "no account"
    /// reachable and "no account" unreachable call for different next steps.
    /// </summary>
    private static string NoAccountMessage(string normalizedEmail, bool canReachCloud) =>
        canReachCloud
            ? $"No account for {normalizedEmail} on this device. Check the address, or create an account to get started."
            : $"There's no account for {normalizedEmail} on this device, and no connection to check your other devices. "
              + "Register to create one - it works without a connection.";

    private readonly record struct AttemptState(int Failures, DateTime LockedUntil);
}