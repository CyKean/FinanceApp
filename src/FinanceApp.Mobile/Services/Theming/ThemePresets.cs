namespace FinanceApp.Mobile.Services.Theming;

/// <summary>
/// Built-in theme presets plus helpers to derive a palette from a custom
/// accent color.
/// </summary>
public static class ThemePresets
{
    public const string DefaultKey = "cream_lime";

    public const string DarkKey = "dark";

    public static readonly ThemePalette CreamLime = new()
    {
        Name = "Finora Cream + Lime",
        IsDark = false,
        Background = "#EFF3DF",
        BackgroundDeep = "#E4EACB",
        Card = "#FAFBF0",
        CardDark = "#FFFFFF",
        Text = "#161B16",
        TextSecondary = "#6F7668",
        Accent = "#CDF463",
        AccentSoft = "#DFFB8D",
        AccentDark = "#A8CF3A",
        Button = "#161B16",
        ButtonText = "#FFFFFF",
        Income = "#3E7C2B",
        IncomeContainer = "#DDF5B8",
        Expense = "#DC2626",
        ExpenseContainer = "#FECACA",
        GradientStart = "#232923",
        GradientMid = "#161B16",
        GradientEnd = "#0E120E",
    };

    public static readonly ThemePalette Dark = new()
    {
        Name = "Midnight",
        IsDark = true,
        Background = "#141814",
        BackgroundDeep = "#0E120E",
        Card = "#1E241E",
        CardDark = "#0E120E",
        Text = "#EFF3DF",
        TextSecondary = "#AEB5A4",
        Accent = "#CDF463",
        AccentSoft = "#3A423A",
        AccentDark = "#A8CF3A",
        Button = "#232923",
        ButtonText = "#EFF3DF",
        Income = "#CDF463",
        IncomeContainer = "#2E352E",
        Expense = "#F87171",
        ExpenseContainer = "#3A2A2A",
        GradientStart = "#232923",
        GradientMid = "#161B16",
        GradientEnd = "#0E120E",
    };

    public static readonly ThemePalette Ocean = new()
    {
        Name = "Ocean Blue",
        IsDark = false,
        Background = "#EAF2F8",
        BackgroundDeep = "#D7E6F0",
        Card = "#F7FBFE",
        CardDark = "#FFFFFF",
        Text = "#0F2434",
        TextSecondary = "#5B7183",
        Accent = "#38BDF8",
        AccentSoft = "#BAE6FD",
        AccentDark = "#0284C7",
        Button = "#0F2434",
        ButtonText = "#FFFFFF",
        Income = "#15803D",
        IncomeContainer = "#DCFCE7",
        Expense = "#DC2626",
        ExpenseContainer = "#FEE2E2",
        GradientStart = "#16405C",
        GradientMid = "#0F2434",
        GradientEnd = "#081A26",
    };

    public static readonly ThemePalette Rose = new()
    {
        Name = "Rose Quartz",
        IsDark = false,
        Background = "#FBEDEF",
        BackgroundDeep = "#F3DCE2",
        Card = "#FFF7F9",
        CardDark = "#FFFFFF",
        Text = "#2B1219",
        TextSecondary = "#8A5A66",
        Accent = "#F472B6",
        AccentSoft = "#FBCFE8",
        AccentDark = "#DB2777",
        Button = "#2B1219",
        ButtonText = "#FFFFFF",
        Income = "#15803D",
        IncomeContainer = "#DCFCE7",
        Expense = "#B91C1C",
        ExpenseContainer = "#FEE2E2",
        GradientStart = "#5C1E33",
        GradientMid = "#2B1219",
        GradientEnd = "#1A0B10",
    };

    public static readonly ThemePalette Forest = new()
    {
        Name = "Forest",
        IsDark = false,
        Background = "#E7F0E8",
        BackgroundDeep = "#D2E3D3",
        Card = "#F5FAF5",
        CardDark = "#FFFFFF",
        Text = "#15251A",
        TextSecondary = "#5C7161",
        Accent = "#4ADE80",
        AccentSoft = "#BBF7D0",
        AccentDark = "#16A34A",
        Button = "#15251A",
        ButtonText = "#FFFFFF",
        Income = "#15803D",
        IncomeContainer = "#DCFCE7",
        Expense = "#DC2626",
        ExpenseContainer = "#FEE2E2",
        GradientStart = "#1F4D2E",
        GradientMid = "#15251A",
        GradientEnd = "#0B1410",
    };

    public static readonly IReadOnlyList<ThemePalette> All = new[] { CreamLime, Dark, Ocean, Rose, Forest };

    public static ThemePalette? Find(string key) => All.FirstOrDefault(p => KeyOf(p) == key);

    public static string KeyOf(ThemePalette p) => p == CreamLime ? DefaultKey :
        p == Dark ? DarkKey :
        p == Ocean ? "ocean" :
        p == Rose ? "rose" :
        p == Forest ? "forest" : p.Name;

    /// <summary>
    /// Derives a full palette from a single accent color. The background is a
    /// very light tint of the accent, the button/ink is the accent darkened
    /// heavily, and the text shade is derived from the ink. Works for both
    /// light and dark bases.
    /// </summary>
    public static ThemePalette FromAccent(string name, string accentHex, bool darkBase)
    {
        var accent = NormalizeHex(accentHex);

        if (darkBase)
        {
            // Dark base: weights below are the accent's share (Mix(a,b,w) = a·w + b·(1−w)).
            var background = Mix(accent, "#0E120E", 0.12);
            var backgroundDeep = Mix(accent, "#000000", 0.06);
            return new ThemePalette
            {
                Name = name,
                IsDark = true,
                Background = background,
                BackgroundDeep = backgroundDeep,
                Card = Mix(accent, "#1E241E", 0.14),
                CardDark = Mix(accent, "#0E120E", 0.08),
                Text = Mix(accent, "#FFFFFF", 0.18),
                TextSecondary = Mix(accent, "#FFFFFF", 0.35),
                Accent = accent,
                AccentSoft = Mix(accent, "#000000", 0.45),
                AccentDark = Mix(accent, "#000000", 0.75),
                Button = Mix(accent, "#232923", 0.2),
                ButtonText = Mix(accent, "#FFFFFF", 0.15),
                Income = accent,
                IncomeContainer = Mix(accent, "#000000", 0.35),
                Expense = "#F87171",
                ExpenseContainer = "#3A2A2A",
                GradientStart = Mix(accent, "#232923", 0.25),
                GradientMid = Mix(accent, "#161B16", 0.15),
                GradientEnd = Mix(accent, "#0E120E", 0.08),
            };
        }

        // Light base: accent shares are small for backgrounds, large for the
        // accent itself. Mix(a,b,w) = a·w + b·(1−w).
        var lightBackground = Mix(accent, "#FFFFFF", 0.10);
        var lightBackgroundDeep = Mix(accent, "#FFFFFF", 0.20);
        var text = Mix(accent, "#000000", 0.18);
        var button = Mix(accent, "#000000", 0.22);
        return new ThemePalette
        {
            Name = name,
            IsDark = false,
            Background = lightBackground,
            BackgroundDeep = lightBackgroundDeep,
            Card = Mix(accent, "#FFFFFF", 0.06),
            CardDark = "#FFFFFF",
            Text = text,
            TextSecondary = Mix(accent, "#4A4A4A", 0.4),
            Accent = accent,
            AccentSoft = Mix(accent, "#FFFFFF", 0.3),
            AccentDark = Mix(accent, "#000000", 0.75),
            Button = button,
            ButtonText = "#FFFFFF",
            Income = Mix(accent, "#15803D", 0.2),
            IncomeContainer = Mix(accent, "#FFFFFF", 0.25),
            Expense = "#DC2626",
            ExpenseContainer = "#FEE2E2",
            GradientStart = Mix(accent, "#000000", 0.25),
            GradientMid = Mix(accent, "#000000", 0.15),
            GradientEnd = Mix(accent, "#000000", 0.08),
        };
    }

    public static bool IsValidHex(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), "^#?([0-9a-fA-F]{6}|[0-9a-fA-F]{3})$");

    public static string NormalizeHex(string value)
    {
        var v = value.Trim();
        if (!v.StartsWith('#')) v = "#" + v;
        if (v.Length == 4)
        {
            v = "#" + v[1] + v[1] + v[2] + v[2] + v[3] + v[3];
        }
        return v.ToUpperInvariant();
    }

    public static (int R, int G, int B) Parse(string hex)
    {
        var v = NormalizeHex(hex).TrimStart('#');
        return (Convert.ToInt32(v[..2], 16), Convert.ToInt32(v[2..4], 16), Convert.ToInt32(v[4..6], 16));
    }

    /// <summary>Returns a color that is <paramref name="weight"/> of <paramref name="a"/> mixed with the rest of <paramref name="b"/>.</summary>
    public static string Mix(string a, string b, double weight)
    {
        var (ar, ag, ab) = Parse(a);
        var (br, bg, bb) = Parse(b);
        int M(int x, int y) => (int)Math.Round(x * weight + y * (1 - weight));
        return $"#{M(ar, br):X2}{M(ag, bg):X2}{M(ab, bb):X2}";
    }

    /// <summary>WCAG 2.x relative luminance contrast ratio.</summary>
    public static double ContrastRatio(string a, string b)
    {
        double Lum(string hex)
        {
            var (r, g, b) = Parse(hex);
            double F(int c)
            {
                var s = c / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * F(r) + 0.7152 * F(g) + 0.0722 * F(b);
        }
        var la = Lum(a);
        var lb = Lum(b);
        var l1 = Math.Max(la, lb);
        var l2 = Math.Min(la, lb);
        return (l1 + 0.05) / (l2 + 0.05);
    }
}
