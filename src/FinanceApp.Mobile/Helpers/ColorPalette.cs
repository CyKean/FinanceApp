namespace FinanceApp.Mobile.Helpers;

/// <summary>
/// Curated swatch set for account / category colors (hex strings).
/// </summary>
public static class ColorPalette
{
    public static IReadOnlyList<string> Swatches { get; } = new[]
    {
        "#0E6B4F", "#16A34A", "#65A30D", "#0E7490",
        "#0891B2", "#0284C7", "#2563EB", "#4F46E5",
        "#7C3AED", "#9333EA", "#C026D3", "#DB2777",
        "#E11D48", "#DC2626", "#EA580C", "#F59E0B",
        "#CA8A04", "#92400E", "#B45309", "#475569",
        "#0F172A", "#065F46", "#9A3412", "#831843"
    };
}
