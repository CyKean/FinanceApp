namespace FinanceApp.Infrastructure.Services;

using FinanceApp.Application.Interfaces;
using FinanceApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

public class AuthenticationService : IAuthenticationService
{
    private readonly DatabaseOptions _options;
    private readonly ILogger<AuthenticationService> _logger;
    private Guid? _currentUserId;
    private string? _currentUserEmail;

    public event Action<AuthStateChangedEventArgs>? AuthStateChanged;

    public AuthenticationService(IOptions<DatabaseOptions> options, ILogger<AuthenticationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing authentication service (stub implementation)");
        
        // TODO: Initialize actual Supabase client
        // await _supabaseClient.InitializeAsync();
        // _supabaseClient.Auth.OnAuthStateChange += OnAuthStateChanged;
        
        await Task.CompletedTask;
        _logger.LogInformation("Authentication service initialized (stub)");
    }

    public async Task<AuthResultDto> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Register user {Email} - stub implementation", email);
        
        // TODO: Implement actual Supabase registration
        // var response = await _supabaseClient.Auth.SignUp(email, password);
        
        // Simulate successful registration for testing
        var userId = Guid.NewGuid();
        _currentUserId = userId;
        _currentUserEmail = email;
        
        AuthStateChanged?.Invoke(new AuthStateChangedEventArgs(true, userId, email));
        
        return new AuthResultDto(true, null, userId, email, "stub-access-token", "stub-refresh-token");
    }

    public async Task<AuthResultDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Login user {Email} - stub implementation", email);
        
        // TODO: Implement actual Supabase login
        // var response = await _supabaseClient.Auth.SignInWithPassword(email, password);
        
        // Simulate successful login for testing
        var userId = Guid.NewGuid();
        _currentUserId = userId;
        _currentUserEmail = email;
        
        AuthStateChanged?.Invoke(new AuthStateChangedEventArgs(true, userId, email));
        
        return new AuthResultDto(true, null, userId, email, "stub-access-token", "stub-refresh-token");
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Logout user - stub implementation");
        
        // TODO: Implement actual Supabase logout
        // await _supabaseClient.Auth.SignOut();
        
        _currentUserId = null;
        _currentUserEmail = null;
        
        AuthStateChanged?.Invoke(new AuthStateChangedEventArgs(false, null, null));
        
        await Task.CompletedTask;
    }

    public async Task<AuthResultDto> RefreshSessionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Refresh session - stub implementation");
        
        // TODO: Implement actual Supabase session refresh
        
        if (_currentUserId.HasValue)
        {
            return new AuthResultDto(true, null, _currentUserId.Value, _currentUserEmail, "stub-access-token", "stub-refresh-token");
        }
        
        return new AuthResultDto(false, "No session to refresh", null, null, null, null);
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Check actual Supabase session
        return _currentUserId.HasValue;
    }

    public async Task<Guid?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
    {
        return _currentUserId;
    }

    public async Task<string?> GetCurrentUserEmailAsync(CancellationToken cancellationToken = default)
    {
        return _currentUserEmail;
    }
}