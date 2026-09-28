namespace FinanceApp.Mobile.Views.Controls;

internal static class ThemeResources
{
    public static bool IsDark =>
        Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;

    public static Color GetColor(string lightKey, string darkKey) =>
        (Color)Microsoft.Maui.Controls.Application.Current!.Resources[IsDark ? darkKey : lightKey];

    public static Brush GetBrush(string lightKey, string darkKey) =>
        (Brush)Microsoft.Maui.Controls.Application.Current!.Resources[(IsDark ? darkKey : lightKey) + "Brush"];
}
