namespace FinanceApp.Application.Services;

using System.Text.Json;

public static class SyncErrorSanitizer
{
    public static string Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Unknown sync error";

        var trimmed = message.Trim();
        if (!trimmed.StartsWith('{'))
            return Humanize(trimmed);

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.TryGetProperty("message", out var error) &&
                error.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(error.GetString()))
                return Humanize(error.GetString()!);
        }
        catch (JsonException)
        {
        }

        return "The server rejected the sync request";
    }

    private static string Humanize(string message)
    {
        if (message.Contains("column", StringComparison.OrdinalIgnoreCase) &&
            (message.Contains("schema cache", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)))
            return "Supabase schema is out of date - run the upgrade SQL at the end of src/FinanceApp.Infrastructure/Supabase/schema.sql";

        // Postgres reports the refusal, not the cause. Every policy in the schema is
        // scoped to the signed-in role and keyed on auth.uid() = user_id, so this
        // almost always means the request went out as the anon key because no
        // session was attached - which the sync engine now catches before sending,
        // and this covers the paths that do reach the server anyway.
        if (message.Contains("row-level security", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("42501", StringComparison.Ordinal))
            return "Supabase rejected the sync because this device was not signed in to the cloud. Sign out and sign in again to reconnect it.";

        return message;
    }
}
