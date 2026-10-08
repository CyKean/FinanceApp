namespace FinanceApp.Mobile.Services.Theming;

using Microsoft.Extensions.Logging;

/// <summary>
/// Owns the active theme: swaps color/brush resources in the application
/// resource dictionary (picked up by DynamicResource references across every
/// page and control), switches RequestedTheme for dark palettes, and persists
/// the choice to <see cref="Preferences"/>.
/// </summary>
public sealed class ThemeService
{
    private const string KeyMode = "financeapp.theme.mode"; // preset | system | custom
    private const string KeyPreset = "financeapp.theme.preset";
    private const string KeyCustomAccent = "financeapp.theme.customAccent";
    private const string KeyCustomDark = "financeapp.theme.customDark";

    private readonly ILogger<ThemeService> _logger;

    public ThemeService(ILogger<ThemeService> logger) => _logger = logger;

    public event EventHandler? ThemeChanged;

    public ThemeMode Mode { get; private set; } = ThemeMode.Preset;

    public string ActivePresetKey { get; private set; } = ThemePresets.DefaultKey;

    public string? CustomAccent { get; private set; }

    public bool CustomDarkBase { get; private set; }

    public ThemePalette Current { get; private set; } = ThemePresets.CreamLime;

    /// <summary>Name shown under App Information > Theme.</summary>
    public string DisplayName => Mode switch
    {
        ThemeMode.Custom => "Custom",
        ThemeMode.System => $"System ({Current.Name})",
        _ => Current.Name,
    };

    public async Task InitializeAsync()
    {
        try
        {
            var mode = Preferences.Get(KeyMode, "preset");
            var preset = Preferences.Get(KeyPreset, ThemePresets.DefaultKey);
            var accent = Preferences.Get(KeyCustomAccent, string.Empty);
            var dark = Preferences.Get(KeyCustomDark, false);

            switch (mode)
            {
                case "custom" when ThemePresets.IsValidHex(accent):
                    ApplyInternal(ThemeMode.Custom, ThemePresets.FromAccent("Custom", accent, dark), persist: false);
                    CustomAccent = ThemePresets.NormalizeHex(accent);
                    CustomDarkBase = dark;
                    break;
                case "system":
                    ApplyInternal(ThemeMode.System, SystemPreset(), persist: false);
                    break;
                default:
                    var found = ThemePresets.Find(preset);
                    ApplyInternal(ThemeMode.Preset, found ?? ThemePresets.CreamLime, persist: false);
                    ActivePresetKey = found is null ? ThemePresets.DefaultKey : ThemePresets.KeyOf(found);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to default theme");
            ApplyInternal(ThemeMode.Preset, ThemePresets.CreamLime, persist: false);
        }
        await Task.CompletedTask;
    }

    public ThemePalette SystemPreset() =>
        (Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark)
            ? ThemePresets.Dark
            : ThemePresets.CreamLime;

    public void ApplyPreset(ThemePalette palette)
    {
        ActivePresetKey = ThemePresets.KeyOf(palette);
        ApplyInternal(ThemeMode.Preset, palette, persist: true);
    }

    public void ApplySystem()
    {
        Mode = ThemeMode.System;
        ApplyInternal(ThemeMode.System, SystemPreset(), persist: true);
    }

    /// <summary>Builds and applies a custom palette from an accent color. Returns an error message when the combination fails the contrast check.</summary>
    public string? ApplyCustom(string accentHex, bool darkBase)
    {
        if (!ThemePresets.IsValidHex(accentHex))
            return "Enter a valid color, e.g. #CDF463.";

        var palette = ThemePresets.FromAccent("Custom", accentHex, darkBase);
        var issue = ValidateContrast(palette);
        if (issue is not null)
            return issue;

        CustomAccent = ThemePresets.NormalizeHex(accentHex);
        CustomDarkBase = darkBase;
        ApplyInternal(ThemeMode.Custom, palette, persist: true);
        return null;
    }

    public void ResetToDefault()
    {
        ActivePresetKey = ThemePresets.DefaultKey;
        CustomAccent = null;
        CustomDarkBase = false;
        ApplyInternal(ThemeMode.Preset, ThemePresets.CreamLime, persist: true);
    }

    public static string? ValidateContrast(ThemePalette palette)
    {
        if (ThemePresets.ContrastRatio(palette.Text, palette.Background) < 4.5)
            return $"Text ({palette.Text}) on background ({palette.Background}) is hard to read. Pick a more distinct accent.";
        if (ThemePresets.ContrastRatio(palette.TextSecondary, palette.Background) < 3.0)
            return $"Secondary text ({palette.TextSecondary}) on background ({palette.Background}) is too low-contrast.";
        if (ThemePresets.ContrastRatio(palette.ButtonText, palette.Button) < 4.5)
            return $"Button text on button color is hard to read. Pick a more distinct accent.";
        return null;
    }

    private void ApplyInternal(ThemeMode mode, ThemePalette palette, bool persist)
    {
        Mode = mode;
        Current = palette;

        if (Microsoft.Maui.Controls.Application.Current is not null)
        {
            var map = palette.ToColorMap();
            UpdateResourceColors(Microsoft.Maui.Controls.Application.Current.Resources, map);
            Microsoft.Maui.Controls.Application.Current.UserAppTheme = mode == ThemeMode.System
                ? AppTheme.Unspecified
                : palette.IsDark ? AppTheme.Dark : AppTheme.Light;
        }

        if (persist)
        {
            Preferences.Set(KeyMode, mode.ToString().ToLowerInvariant());
            Preferences.Set(KeyPreset, mode == ThemeMode.Preset ? ActivePresetKey : string.Empty);
            Preferences.Set(KeyCustomAccent, mode == ThemeMode.Custom ? CustomAccent ?? string.Empty : string.Empty);
            Preferences.Set(KeyCustomDark, CustomDarkBase);
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void UpdateResourceColors(ResourceDictionary resources, IReadOnlyDictionary<string, string> map)
    {
        var colors = new Dictionary<string, Color>();
        foreach (var entry in map)
        {
            if (TryParseColor(entry.Value, out var color))
                colors[entry.Key] = color;
        }

        // DynamicResource subscriptions are notified from the resource
        // dictionary that owns the binding context (the application root), so
        // the new values must live on that root dictionary to re-render.
        // Root keys win over merged-dictionary entries during lookup.
        foreach (var key in colors.Keys)
        {
            resources[key] = colors[key];
            var brushKey = key + "Brush";
            if (resources.MergedDictionaries.Any(d => d.ContainsKey(brushKey)))
                resources[brushKey] = new SolidColorBrush(colors[key]);
        }
    }

    private static bool TryParseColor(string hex, out Color color)
    {
        try
        {
            color = Color.FromArgb(hex);
            return true;
        }
        catch
        {
            color = Colors.Magenta;
            return false;
        }
    }
}

public enum ThemeMode
{
    Preset,
    System,
    Custom,
}
