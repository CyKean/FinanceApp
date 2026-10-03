namespace FinanceApp.Application.Interfaces;

/// <summary>
/// On-device credential store. This is the source of truth for register and
/// sign-in, so both work with no network connection.
/// </summary>
public interface ILocalAccountStore
{
    /// <summary>Case-insensitive lookup. Null when the email has no local account.</summary>
    Task<LocalAccountDto?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many accounts this device holds. Diagnostics only - it distinguishes
    /// "wrong email" from "this install has no accounts" when a sign-in misses.
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a local account, hashing the password. Throws when the email is taken.</summary>
    Task<LocalAccountDto> CreateAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a local account under an externally issued id (a verified Supabase
    /// uuid), so cached cloud accounts already match <c>auth.uid()</c>.
    /// </summary>
    Task<LocalAccountDto> CreateAsync(string email, string password, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adopts an externally issued identity (Supabase uuid) for an existing account.</summary>
    Task ReassignIdAsync(Guid currentId, Guid newId, CancellationToken cancellationToken = default);

    Task RecordSignInAsync(Guid userId, CancellationToken cancellationToken = default);
}

public record LocalAccountDto(
    Guid Id,
    string Email,
    string PasswordHash,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

/// <summary>
/// One-way password hashing. Implementations must be salted and constant-time.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string encodedHash);
}