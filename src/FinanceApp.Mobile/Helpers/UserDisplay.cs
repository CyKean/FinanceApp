namespace FinanceApp.Mobile.Helpers;

/// <summary>
/// Turns a sign-in email into a friendly display name and avatar initial.
/// IAuthenticationService only exposes the email, so the local part of the
/// address is used as the name: "maria.santos@example.com" -> "Maria Santos".
/// </summary>
public static class UserDisplay
{
    public const string FallbackName = "Guest";
    public const string FallbackInitial = "G";

    private static readonly char[] Separators = { '.', '_', '-', '+', ' ' };

    public static string NameFromEmail(string? email)
    {
        if (!TryGetLocalPart(email, out var localPart))
            return FallbackName;

        var words = localPart.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return FallbackName;

        return string.Join(' ', words.Select(TitleCase));
    }

    public static string InitialFromEmail(string? email)
    {
        var name = NameFromEmail(email);
        return name.Length > 0 ? name[..1].ToUpperInvariant() : FallbackInitial;
    }

    private static bool TryGetLocalPart(string? email, out string localPart)
    {
        localPart = string.Empty;

        if (string.IsNullOrWhiteSpace(email))
            return false;

        // Everything from '@' onwards is the host; '+' sub-addressing is
        // already treated as a separator above, so the local part can be
        // sliced at the first '@'.
        var at = email.IndexOf('@');
        localPart = (at >= 0 ? email[..at] : email).Trim();

        return localPart.Length > 0;
    }

    private static string TitleCase(string word) =>
        word.Length == 1
            ? word.ToUpperInvariant()
            : string.Concat(char.ToUpperInvariant(word[0]), word[1..].ToLowerInvariant());
}
