namespace FinanceApp.Mobile.Services.Theming;

/// <summary>
/// A theme expressed as a small set of semantic slots. The slot values are
/// fanned out onto the many resource keys the app has historically used
/// (FinoraCream, Surface, Primary, ...) so every screen updates at once.
/// </summary>
public sealed record ThemePalette
{
    public required string Name { get; init; }
    public required bool IsDark { get; init; }

    public required string Background { get; init; }
    public required string BackgroundDeep { get; init; }
    public required string Card { get; init; }
    public required string CardDark { get; init; }
    public required string Text { get; init; }
    public required string TextSecondary { get; init; }
    public required string Accent { get; init; }
    public required string AccentSoft { get; init; }
    public required string AccentDark { get; init; }
    public required string Button { get; init; }
    public required string ButtonText { get; init; }
    public required string Income { get; init; }
    public required string IncomeContainer { get; init; }
    public required string Expense { get; init; }
    public required string ExpenseContainer { get; init; }
    public required string GradientStart { get; init; }
    public required string GradientMid { get; init; }
    public required string GradientEnd { get; init; }

    // A few "semantic" slots that exist for backwards compatibility with the
    // Material keys in Colors.xaml. They default to sensible values derived
    // from the main slots and can be overridden per-preset.
    public string? PrimaryOverride { get; init; }

    public string ButtonOrPrimary => PrimaryOverride ?? Button;

    public IReadOnlyDictionary<string, string> ToColorMap(bool? darkVariant = null)
    {
        var button = ButtonOrPrimary;
        var map = new Dictionary<string, string>
        {
            // Finora tokens
            ["FinoraCream"] = Background,
            ["FinoraCreamDeep"] = BackgroundDeep,
            ["FinoraCard"] = Card,
            ["FinoraInk"] = Text,
            ["FinoraInkSoft"] = Button,
            ["FinoraMuted"] = TextSecondary,
            ["FinoraLine"] = Text,
            ["FinoraWhite"] = IsDark ? Card : "#FFFFFF",
            ["FinoraLime"] = Accent,
            ["FinoraLimeSoft"] = AccentSoft,
            ["FinoraLimeDark"] = AccentDark,

            // Material surface keys
            ["Background"] = Background,
            ["OnBackground"] = Text,
            ["Surface"] = Card,
            ["SurfaceDim"] = BackgroundDeep,
            ["SurfaceBright"] = IsDark ? Card : "#FFFFFF",
            ["SurfaceContainerLowest"] = IsDark ? CardDark : "#FFFFFF",
            ["SurfaceContainerLow"] = Card,
            ["SurfaceContainer"] = Background,
            ["SurfaceContainerHigh"] = BackgroundDeep,
            ["SurfaceContainerHighest"] = BackgroundDeep,
            ["OnSurface"] = Text,
            ["OnSurfaceVariant"] = TextSecondary,
            ["Outline"] = Text,
            ["OutlineVariant"] = BackgroundDeep,
            ["InverseSurface"] = Button,
            ["InverseOnSurface"] = Background,
            ["InversePrimary"] = Accent,
            ["SurfaceTint"] = button,

            // Primary / secondary
            ["Primary"] = button,
            ["PrimaryLight"] = Accent,
            ["PrimaryDark"] = "#0E120E",
            ["PrimaryContainer"] = Accent,
            ["OnPrimary"] = ButtonText,
            ["OnPrimaryContainer"] = Text,
            ["Secondary"] = TextSecondary,
            ["SecondaryLight"] = Accent,
            ["SecondaryContainer"] = BackgroundDeep,
            ["OnSecondary"] = ButtonText,
            ["OnSecondaryContainer"] = Text,
            ["Tertiary"] = AccentDark,
            ["TertiaryContainer"] = AccentSoft,
            ["OnTertiary"] = ButtonText,
            ["OnTertiaryContainer"] = Text,

            // Semantic
            ["Error"] = Expense,
            ["ErrorContainer"] = ExpenseContainer,
            ["OnError"] = "#FFFFFF",
            ["OnErrorContainer"] = IsDark ? Text : "#450A0A",
            ["Success"] = Income,
            ["SuccessContainer"] = IncomeContainer,
            ["OnSuccess"] = "#FFFFFF",
            ["OnSuccessContainer"] = IsDark ? Text : "#1A2E10",
            ["Warning"] = "#D97706",
            ["WarningContainer"] = "#FEF3C7",
            ["OnWarning"] = "#000000",
            ["OnWarningContainer"] = "#451A03",
            ["Info"] = "#0284C7",
            ["InfoContainer"] = "#E0F2FE",
            ["OnInfo"] = "#FFFFFF",
            ["OnInfoContainer"] = "#082F49",

            // Gradients
            ["GradientStart"] = GradientStart,
            ["GradientMid"] = GradientMid,
            ["GradientEnd"] = GradientEnd,
            ["GradientGlow"] = Accent,
            ["IncomeAccent"] = Accent,
            ["ExpenseAccent"] = ExpenseContainer,
            ["GlassWhite"] = "#26FFFFFF",
            ["GlassWhiteStrong"] = "#3DFFFFFF",
            ["CardSurface"] = Card,
            ["CardStroke"] = Text,

            // Extended tokens
            ["MutedSoft"] = ThemePresets.Mix(TextSecondary, Background, 0.55),
            ["DividerDark"] = ThemePresets.Mix(Text, Background, 0.45),
            ["AccentGlow"] = AccentSoft,
            ["Sand"] = BackgroundDeep,
            ["LineSoft"] = BackgroundDeep,
            ["TabBarUnselected"] = TextSecondary,
            ["DangerContainer"] = ExpenseContainer,

            // Dark-suffixed variants get the same palette so ThemeResources
            // and AppThemeBinding setters resolve consistently.
            ["SurfaceDark"] = Background,
            ["SurfaceContainerLowestDark"] = CardDark,
            ["SurfaceContainerLowDark"] = Card,
            ["SurfaceContainerDark"] = Button,
            ["SurfaceContainerHighDark"] = BackgroundDeep,
            ["SurfaceContainerHighestDark"] = BackgroundDeep,
            ["OnSurfaceDark"] = Text,
            ["OnSurfaceVariantDark"] = TextSecondary,
            ["OutlineDark"] = BackgroundDeep,
            ["OutlineVariantDark"] = BackgroundDeep,
            ["BackgroundDark"] = Background,
            ["OnBackgroundDark"] = Text,
            ["PrimaryDarkVariant"] = Accent,
            ["PrimaryLightDark"] = Accent,
            ["PrimaryContainerDark"] = Button,
            ["OnPrimaryContainerDark"] = Text,
            ["PrimaryDarkText"] = Text,
            ["SecondaryDarkText"] = Accent,
            ["CardSurfaceDark"] = CardDark,
            ["CardStrokeDark"] = BackgroundDeep,
        };
        return map;
    }
}
