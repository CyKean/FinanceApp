namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Helpers;
/// <summary>
/// Builds the icon-choice grid used by the Add/Edit forms: mockup-style
/// black-circle line-icon tiles instead of raw emoji. The selected tile
/// gets a lime badge with an ink ring.
/// </summary>
public static class FinoraIconPicker
{
    private static Color Ink => FinoraOverlay.Resolve("FinoraInk", "#161B16");

    public static void Build(FlexLayout container, IEnumerable<string> emojis, string? selectedIcon, Action<string> onSelect)
    {
        container.Children.Clear();
        var selected = (selectedIcon ?? string.Empty).Trim();
        foreach (var emoji in emojis)
        {
            var isSelected = string.Equals((emoji ?? string.Empty).Trim(), selected, StringComparison.Ordinal);
            var badge = new FinoraIconView
            {
                Icon = emoji,
                Size = 42,
                Light = isSelected,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            };
            var tile = new Border
            {
                WidthRequest = 56,
                HeightRequest = 56,
                StrokeThickness = isSelected ? 1.5 : 0,
                Stroke = isSelected ? new SolidColorBrush(Ink) : Brush.Transparent,
                BackgroundColor = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                Content = badge,
                Margin = new Thickness(2),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            };
            var captured = emoji;
            tile.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() => onSelect(captured))
            });
            container.Children.Add(tile);
        }
    }
}
