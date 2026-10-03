namespace FinanceApp.UnitTests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FinanceApp.Application.Interfaces;
using FinanceApp.Infrastructure.Configuration;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Services;
using FinanceApp.Infrastructure.Supabase;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Registration and sign-in must work on a device with no network and no
/// backend configured at all - that is the offline-first guarantee.
/// <para>
/// The service under test is wired against a real (in-memory) SQLite database
/// with an empty <see cref="DatabaseOptions"/>, so <c>SupabaseClientProvider</c>
/// reports itself unconfigured and every cloud path is skipped, exactly as it
/// is on a plane. Nothing here is mocked except the secure key/value store.
/// </para>
/// </summary>
public class OfflineAuthenticationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly FakeSessionStore _sessionStore = new();
    private readonly SupabaseClientProvider _clientProvider;

    public OfflineAuthenticationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_connection);
        services.AddDbContext<FinanceAppDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));
        services.AddScoped<ILocalAccountStore, LocalAccountStore>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        _provider = services.BuildServiceProvider();

        // No URL and no anon key => IsConfigured is false. No HTTP is possible.
        _clientProvider = new SupabaseClientProvider(
            Options.Create(new DatabaseOptions()),
            NullLogger<SupabaseClientProvider>.Instance);
    }

    [Fact]
    public async Task RegisterAsync_CreatesAnAccountAndSignsIn()
    {
        var service = CreateService();

        var result = await service.RegisterAsync("ada@example.com", "correct horse");

        Assert.True(result.Success);
        Assert.Equal("ada@example.com", result.Email);
        Assert.NotNull(result.UserId);
        Assert.True(await service.IsAuthenticatedAsync());
        Assert.Equal(result.UserId, await service.GetCurrentUserIdAsync());
    }

    [Fact]
    public async Task RegisterAsync_NeverStoresThePasswordItself()
    {
        var service = CreateService();

        await service.RegisterAsync("ada@example.com", "correct horse");

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FinanceAppDbContext>();
        var stored = await context.LocalUsers.SingleAsync();

        Assert.DoesNotContain("correct horse", stored.PasswordHash);
        Assert.StartsWith("$pbkdf2-sha256$", stored.PasswordHash);
    }

    [Fact]
    public async Task RegisterAsync_DoesNotQueueCredentialsForCloudSync()
    {
        var service = CreateService();

        await service.RegisterAsync("ada@example.com", "correct horse");

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FinanceAppDbContext>();

        // The DbContext outbox must stay empty: a password hash has no business
        // being pushed to Supabase.
        Assert.Empty(await context.SyncOperations.ToListAsync());
    }

    [Fact]
    public async Task RegisterAsync_RejectsASecondAccountForTheSameEmail()
    {
        var service = CreateService();
        await service.RegisterAsync("ada@example.com", "correct horse");

        var duplicate = await CreateService().RegisterAsync("ADA@Example.com", "another password");

        Assert.False(duplicate.Success);
        Assert.Contains("already exists", duplicate.ErrorMessage);
    }

    [Fact]
    public async Task RegisterAsync_RejectsAShortPassword()
    {
        var result = await CreateService().RegisterAsync("ada@example.com", "12345");

        Assert.False(result.Success);
        Assert.Contains("6 characters", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("ada@")]
    [InlineData("@example.com")]
    public async Task RegisterAsync_RejectsAnInvalidEmail(string email)
    {
        var result = await CreateService().RegisterAsync(email, "correct horse");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task LoginAsync_AcceptsThePasswordChosenAtRegistration()
    {
        var registered = await CreateService().RegisterAsync("ada@example.com", "correct horse");

        var result = await CreateService().LoginAsync("ada@example.com", "correct horse");

        Assert.True(result.Success);
        Assert.Equal(registered.UserId, result.UserId);
    }

    [Fact]
    public async Task LoginAsync_RejectsAWrongPassword()
    {
        var registering = CreateService();
        await registering.RegisterAsync("ada@example.com", "correct horse");
        // Sign out first: otherwise the remembered session (not the failed
        // login) is what keeps the account authenticated.
        await registering.LogoutAsync();

        var service = CreateService();
        var result = await service.LoginAsync("ada@example.com", "wrong horse");

        Assert.False(result.Success);
        Assert.Equal("Email or password is incorrect.", result.ErrorMessage);
        Assert.False(await service.IsAuthenticatedAsync());
    }

    [Fact]
    public async Task LoginAsync_SaysSoWhenThereIsNoAccountOnThisDevice()
    {
        var result = await CreateService().LoginAsync("grace@example.com", "correct horse");

        Assert.False(result.Success);
        Assert.Contains("no account for grace@example.com on this device", result.ErrorMessage);
        // Nothing else can vouch for this email, so the message has to point at
        // registering rather than leave the user hunting for a password they
        // never set.
        Assert.Contains("Register to create one", result.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_ForAMissingAccountNamesTheOtherAccountsOnTheDevice()
    {
        var service = CreateService();
        await service.RegisterAsync("ada@example.com", "correct horse");
        await service.LogoutAsync();

        var result = await CreateService().LoginAsync("grace@example.com", "correct horse");

        Assert.False(result.Success);
        Assert.Contains("Register to create one", result.ErrorMessage);
    }

    [Fact]
    public async Task CountAsync_ReflectsTheAccountsStoredOnTheDevice()
    {
        using var scope = _provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ILocalAccountStore>();

        Assert.Equal(0, await store.CountAsync());

        await CreateService().RegisterAsync("ada@example.com", "correct horse");

        using var secondScope = _provider.CreateScope();
        Assert.Equal(1, await secondScope.ServiceProvider.GetRequiredService<ILocalAccountStore>().CountAsync());
    }

    [Fact]
    public async Task LoginAsync_LocksOutAfterRepeatedFailures()
    {
        await CreateService().RegisterAsync("ada@example.com", "correct horse");

        var service = CreateService();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await service.LoginAsync("ada@example.com", "wrong horse");
            Assert.False(failed.Success);
        }

        // The correct password is refused too, which is the point of the lockout.
        var lockedOut = await service.LoginAsync("ada@example.com", "correct horse");

        Assert.False(lockedOut.Success);
        Assert.Contains("Too many failed attempts", lockedOut.ErrorMessage);
    }

    [Fact]
    public async Task RememberedSessionIsRestoredOnTheNextLaunch()
    {
        var firstLaunch = CreateService();
        var registered = await firstLaunch.RegisterAsync("ada@example.com", "correct horse");
        await firstLaunch.LogoutAsync();

        // Sign in again so a session is actually remembered, then simulate a restart.
        await firstLaunch.LoginAsync("ada@example.com", "correct horse");
        var secondLaunch = CreateService();

        await secondLaunch.InitializeAsync();

        Assert.True(await secondLaunch.IsAuthenticatedAsync());
        Assert.Equal(registered.UserId, await secondLaunch.GetCurrentUserIdAsync());
        Assert.Equal("ada@example.com", await secondLaunch.GetCurrentUserEmailAsync());
    }

    [Fact]
    public async Task LogoutAsync_ClearsTheSession()
    {
        var service = CreateService();
        await service.RegisterAsync("ada@example.com", "correct horse");

        await service.LogoutAsync();

        Assert.False(await service.IsAuthenticatedAsync());
        Assert.Null(await service.GetCurrentUserIdAsync());
    }

    [Fact]
    public async Task RefreshSessionAsync_SucceedsWhileOffline()
    {
        var service = CreateService();
        var registered = await service.RegisterAsync("ada@example.com", "correct horse");

        var refreshed = await service.RefreshSessionAsync();

        Assert.True(refreshed.Success);
        Assert.Equal(registered.UserId, refreshed.UserId);
    }

    [Fact]
    public async Task AuthStateChanged_FiresOnSignInAndSignOut()
    {
        var service = CreateService();
        var events = new List<AuthStateChangedEventArgs>();
        service.AuthStateChanged += events.Add;

        await service.RegisterAsync("ada@example.com", "correct horse");
        await service.LogoutAsync();

        Assert.Equal(2, events.Count);
        Assert.True(events[0].IsAuthenticated);
        Assert.Equal("ada@example.com", events[0].Email);
        Assert.False(events[1].IsAuthenticated);
    }

    private AuthenticationService CreateService() => new(
        _clientProvider,
        _sessionStore,
        _provider.GetRequiredService<IServiceScopeFactory>(),
        new PasswordHasher(),
        NullLogger<AuthenticationService>.Instance);

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class FakeSessionStore : ISessionStore
    {
        private readonly Dictionary<string, string> _values = new();

        public Task SaveAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> LoadAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }
}