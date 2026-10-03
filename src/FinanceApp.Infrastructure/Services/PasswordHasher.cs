namespace FinanceApp.Infrastructure.Services;

using System.Security.Cryptography;
using FinanceApp.Application.Interfaces;

/// <summary>
/// PBKDF2-HMAC-SHA256 with a per-account random salt.
/// The encoded digest carries its own algorithm and iteration count
/// ("$pbkdf2-sha256$&lt;iterations&gt;$&lt;salt&gt;$&lt;hash&gt;") so the work factor
/// can be raised later without invalidating credentials already on disk.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const string Prefix = "$pbkdf2-sha256$";
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, SaltSize);

        return string.Concat(
            Prefix,
            Iterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "$",
            Convert.ToBase64String(salt),
            "$",
            Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(encodedHash))
            return false;

        var parts = encodedHash.Split('$');
        // Leading '$' means an empty first element: ["", "pbkdf2-sha256", iters, salt, hash]
        if (parts.Length != 5 ||
            parts[0].Length != 0 ||
            !string.Equals(parts[1], "pbkdf2-sha256", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(parts[2], out var iterations) ||
            iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expected.Length == 0)
            return false;

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);

        // Constant-time: a length or byte mismatch must not leak through timing.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}