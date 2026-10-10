namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;
using static FinanceApp.Mobile.Views.Controls.SkeletonShapes;

/// <summary>
/// Budgets: header, month stepper, the AI suggestions banner, then budget cards
/// and the Add Budget button.
/// <para>
/// The page's card is 122 tall (or 138 once the over-budget line appears)
/// because of the three-column Spent/Remaining/Budget strip under the header
/// row. The old placeholder was 104 and put the progress bar in the footer,
/// where the page has none - it sits beside the category name.
/// </para>
/// </summary>
public class SkeletonBudgets : SkeletonPage
{
    public SkeletonBudgets()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 100) })
    {
    }

    protected override void Populate()
    {
        Host.Children.Add(SkeletonShapes.PageHeader(
            trailing: SkeletonShapes.ActionPill(88, 34),
            titleWidth: 82,
            subtitleWidth: 196));

        Host.Children.Add(SkeletonShapes.MonthNavigator());

        Host.Children.Add(BuildSuggestionsBanner());

        var rows = SkeletonShapes.VStack(8);
        for (var i = 0; i < 4; i++)
        {
            rows.Add(BuildCard(i));
        }

        Host.Children.Add(rows);

        Host.Children.Add(SkeletonShapes.ActionButton(56));
    }

    /// <summary>The dark "AI budget suggestions" strip above the list.</summary>
    private static View BuildSuggestionsBanner()
    {
        var copy = SkeletonShapes.Filling(0,
            SkeletonShapes.Bar(190, 14, width: 132, fill: SkeletonPalette.OnDark),
            SkeletonShapes.Bar(190, 11, width: 158, fill: SkeletonPalette.OnDark));

        var chevron = Bar(190, 18, width: 12, fill: SkeletonPalette.OnDark);
        chevron.VerticalOptions = LayoutOptions.Center;

        var content = SkeletonShapes.HStack(10, SkeletonShapes.Circle(40, SkeletonPalette.OnDark), copy, chevron);

        return new Border
        {
            BackgroundColor = SkeletonPalette.Ink,
            Stroke = new SolidColorBrush(SkeletonPalette.Ink),
            StrokeThickness = 1.5,
            Padding = new Thickness(16, 12),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24) },
            Content = content
        };
    }

    /// <summary>
    /// Header row (icon, name over its progress bar, percentage pill) above the
    /// three stat columns. 48 + 10 + 32 + 32 of padding = 122.
    /// </summary>
    private static View BuildCard(int index)
    {
        var heading = SkeletonShapes.Filling(6,
            SkeletonShapes.Bar(190, 15, width: index % 2 == 0 ? 104 : 82),
            SkeletonShapes.Bar(190, 8, width: index % 3 == 1 ? 108 : 132));

        var pill = Bar(190, 16, width: 38, fill: SkeletonPalette.Lime);
        pill.VerticalOptions = LayoutOptions.Center;

        var header = SkeletonShapes.HStack(12, SkeletonShapes.IconDisc(48), heading, pill);

        var stats = SkeletonShapes.HStack(8,
            SkeletonShapes.Filling(2,
                SkeletonShapes.Bar(190, 11, width: 40),
                SkeletonShapes.Bar(190, 13, width: 62)),
            SkeletonShapes.Filling(2,
                SkeletonShapes.Bar(190, 11, width: 62),
                SkeletonShapes.Bar(190, 13, width: 58)),
            SkeletonShapes.Filling(2,
                SkeletonShapes.Bar(190, 11, width: 44),
                SkeletonShapes.Bar(190, 13, width: 66)));

        return SkeletonShapes.Row(122, 24, SkeletonShapes.VStack(10, header, stats), padding: 16);
    }
}