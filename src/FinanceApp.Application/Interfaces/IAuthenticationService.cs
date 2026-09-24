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