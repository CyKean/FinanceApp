namespace FinanceApp.Application.Interfaces;

public interface IAuthenticationService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<AuthResultDto> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<AuthResultDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<AuthResultDto> RefreshSessionAsync(CancellationToken cancellationToken = default);
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);
    Task<Guid?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);
    Task<string?> GetCurrentUserEmailAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the Supabase account for this device exists but has not confirmed
    /// its email. Those accounts cannot receive a session, so sync is paused until
    /// the verification email is opened; no token to load.
    /// </summary>
    Task<bool> IsEmailVerificationRequiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks Supabase to email the verification link again. There is nothing local
    /// to do until the email is opened and the user signs in.
    /// </summary>
    Task<bool> ResendEmailVerificationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// When true (default), successful login/register persists the session
    /// so the user is automatically signed in on next launch.
    /// </summary>
    bool RememberMe { get; set; }

    event Action<AuthStateChangedEventArgs>? AuthStateChanged;
}

public record AuthResultDto(
    bool Success,
    string? ErrorMessage,
    Guid? UserId,
    string? Email,
    string? AccessToken,
    string? RefreshToken
);

public record AuthStateChangedEventArgs(
    bool IsAuthenticated,
    Guid? UserId,
    string? Email
);