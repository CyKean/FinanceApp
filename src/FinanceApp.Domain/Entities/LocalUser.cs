namespace FinanceApp.Domain.Entities;

/// <summary>
/// A credential that lives only on this device. Registration and sign-in are
/// resolved against this table, which is what makes the app offline-first:
/// a user can create an account and sign back in with no network at all.
///
/// Deliberately NOT derived from <see cref="Common.Entity"/> - credentials must
/// never enter the Supabase sync outbox, and a local account outlives any
/// individual sync run.
/// </summary>
public class LocalUser
{
    public Guid Id { get; set; }

    /// <summary>Lower-cased and trimmed; unique so one account per address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Self-describing PBKDF2 digest. Never the raw password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
}