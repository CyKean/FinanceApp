namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;
using static FinanceApp.Mobile.Views.Controls.SkeletonShapes;

/// <summary>
/// Goals: header, the lime "Total saved" hero, then goal cards and the Add Goal
/// button.
/// <para>
/// A goal card is by some way the tallest in the app: 50 for the icon row, 46
/// for the Current/Target/Left tiles, 40 for the Edit-and-plus action row, plus
/// 20 of spacing and 32 of padding - 188. The old placeholder was 112 and had
/// two sections instead of three, which is a 76pt jump on every single card.
/// </para>
/// </summary>
public class SkeletonGoals : SkeletonPage
{
    public SkeletonGoals()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 24) })
    {
    }

    protected override void Populate()
    {
        Host.Children.Add(SkeletonShapes.PageHeader(
            trailing: SkeletonShapes.ActionPill(40, 34),
            titleWidth: 62,
            subtitleWidth: 168));

        Host.Children.Add(BuildSavingsHero());

        var rows = SkeletonShapes.VStack(8);
        for (var i = 0; i < 3; i++)
        {
            rows.Add(BuildCard(i));
        }

        Host.Children.Add(rows);

        Host.Children.Add(SkeletonShapes.ActionButton(56));
    }

    /// <summary>The lime card above the list, at the page's own padding of 18.</summary>
    private static View BuildSavingsHero()
    {
        var copy = VStack(4,
            Bar(190, 12, width: 96, fill: SkeletonPalette.Ink),
            Bar(190, 24, width: 168, fill: SkeletonPalette.Ink),
            Bar(190, 11, width: 190, fill: SkeletonPalette.Ink));

        return new Border
        {
            BackgroundColor = SkeletonPalette.Lime,
            Stroke = new SolidColorBrush(SkeletonPalette.Ink),
            StrokeThickness = 1.5,
            Padding = new Thickness(18),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28) },
            Content = copy
        };
    }

    /// <summary>
    /// Three stacked bands: the icon row with its progress bar, the stat tiles,
    /// and the action row. 50 + 10 + 46 + 10 + 40 + 32 = 188.
    /// </summary>
    private static View BuildCard(int index)
    {
        var heading = SkeletonShapes.Filling(6,
            SkeletonShapes.Bar(190, 15, width: index % 3 == 2 ? 88 : 116),
            SkeletonShapes.Bar(190, 8, width: index % 2 == 0 ? 124 : 96));

        var pill = Bar(190, 16, width: 38, fill: SkeletonPalette.Lime);
        pill.VerticalOptions = LayoutOptions.Center;

        var iconRow = SkeletonShapes.HStack(12, SkeletonShapes.IconDisc(50), heading, pill);

        var tiles = SkeletonShapes.HStack(8,
            BuildTile(58, SkeletonPalette.Block),
            BuildTile(74, SkeletonPalette.Block),
            BuildTile(64, SkeletonPalette.Ink));

        var actions = SkeletonShapes.HStack(8,
            Bar(190, 12, width: 74),
            SkeletonShapes.Filler(),
            Slab(40, width: 72, fill: SkeletonPalette.Surface, radius: 20),
            Slab(40, width: 48, fill: SkeletonPalette.Lime, radius: 20));

        var content = SkeletonShapes.VStack(10, iconRow, tiles, actions);
        return SkeletonShapes.Row(188, 24, content, padding: 16);
    }

    /// <summary>One of the Current/Target/Left tiles, filled like the page's.</summary>
    private static View BuildTile(double captionWidth, Color fill)
    {
        var caption = Bar(190, 10, width: captionWidth, fill: fill);
        var value = Bar(190, 12, width: captionWidth - 16, fill: fill);

        var inner = VStack(2, caption, value);
        inner.HorizontalOptions = LayoutOptions.Start;

        return new Border
        {
            BackgroundColor = fill,
            StrokeThickness = 0,
            Padding = new Thickness(10, 8),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(16) },
            Content = inner
        };
    }
}