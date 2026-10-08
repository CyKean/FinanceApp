namespace FinanceApp.Mobile.Helpers;

using FinanceApp.Mobile.Services;

/// <summary>
/// Shared design tokens for the page-level overlays (toast, success/failure,
/// confirm, choice sheet).
/// <para>
/// These controls each used to hardcode Material palette values and text glyphs,
/// which drifted from the Finora theme used by the rest of the app. Everything
/// resolves from the app resources here instead, so one edit re-skins them all.
/// </para>
/// </summary>
public static class FinoraOverlay
{
    // Fallbacks used only if a resource lookup fails (e.g. before App loads).
    private const string InkKey = "FinoraInk";
    private const string LimeKey = "FinoraLime";

    /// <summary>Primary text and outline strokes.</summary>
    public static Color Ink => Resolve(InkKey, "#161B16");

    /// <summary>Accent for positive confirmation.</summary>
    public static Color Lime => Resolve(LimeKey, "#CDF463");

    /// <summary>
    /// Error accent. Kept outside the Finora palette so failures read as
    /// failures, and inline here because the Finora palette has no red.
    /// </summary>
    private static Color ErrorAccent => Resolve("Error", "#DC2626");

    // Motion timings, kept together so every overlay moves at the same pace.
    public const uint ScrimFadeMs = 180;
    public const uint CardInMs = 240;
    public const uint CardOutMs = 180;
    public const uint SpringMs = 320;
    public const uint ToastInMs = 220;
    public const uint ToastOutMs = 180;
    public const double CardScaleIn = 0.94;
    public const double CardScaleOut = 0.96;

    /// <summary>How long a toast stays on screen.</summary>
    public static TimeSpan ToastDuration => TimeSpan.FromSeconds(2.6);

    /// <summary>How long the success/failure overlay holds before dismissing.</summary>
    public static TimeSpan HoldDuration => TimeSpan.FromMilliseconds(800);

    /// <summary>Failures linger a little longer than successes.</summary>
    public static TimeSpan FailureHoldDuration => TimeSpan.FromMilliseconds(950);

    /// <summary>
    /// Accent + Lucide glyph for a toast kind. Lucide keys rather than text
    /// glyphs so they match every other icon in the app.
    /// </summary>
    public static (Color Accent, Color OnAccent, string Glyph) ForToast(ToastKind kind) => kind switch
    {
        ToastKind.Error => (ErrorAccent, Colors.White, "alert"),
        ToastKind.Info => (Ink, Lime, "bell"),
        _ => (Lime, Ink, "check")
    };

    /// <summary>
    /// Accent + Lucide glyph for a confirm prompt. A destructive action keeps a
    /// red badge and uses the warning triangle rather than a tick.
    /// </summary>
    public static (Color Accent, Color OnAccent, string Glyph) ForConfirm(bool destructive) =>
        destructive ? (ErrorAccent, Colors.White, "alert") : (Lime, Ink, "check");

    /// <summary>Accent + Lucide glyph for the success/failure overlay.</summary>
    public static (Color Accent, Color OnAccent, string Glyph) ForResult(bool failed) =>
        failed ? (ErrorAccent, Colors.White, "alert") : (Lime, Ink, "check");

    /// <summary>
    /// Resolves a colour from the app resources, walking the merged
    /// dictionaries, then falling back to <paramref name="fallbackHex"/>.
    /// </summary>
    public static Color Resolve(string key, string fallbackHex)
    {
        var resources = Microsoft.Maui.Controls.Application.Current?.Resources;
        if (resources is not null)
        {
            if (resources.TryGetValue(key, out var direct) && direct is Color directColor)
                return directColor;

            foreach (var dictionary in resources.MergedDictionaries)
            {
                if (dictionary.TryGetValue(key, out var merged) && merged is Color mergedColor)
                    return mergedColor;
            }
        }

        return Color.FromArgb(fallbackHex);
    }
}
