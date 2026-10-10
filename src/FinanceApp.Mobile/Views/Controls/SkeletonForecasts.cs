namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;
using static FinanceApp.Mobile.Views.Controls.SkeletonShapes;

/// <summary>
/// Predictions: header, the dark prediction hero, then the five section cards -
/// Category Breakdown, Spending Trends, Budget Forecasts, Smart Insights and
/// Unusual Spending.
/// <para>
/// The old placeholder had a trend chart that this page does not contain, and
/// only two of the five sections. It also omitted the header. Four of the five
/// cards are lists, so each is a heading plus a couple of rows.
/// </para>
/// </summary>
public class SkeletonForecasts : SkeletonPage
{
    public SkeletonForecasts()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 24) })
    {
    }

/// <summary>Row shape for one section: height, icon size, lead and trail bar widths.</summary>
    private readonly record struct RowSpec(double RowHeight, double Icon, double Lead, double Trail);

    protected override void Populate()
    {
        Host.Children.Add(SkeletonShapes.PageHeader(
            trailing: SkeletonShapes.Circle(46, SkeletonPalette.Surface),
            titleWidth: 88,
            subtitleWidth: 166));

        Host.Children.Add(BuildHero());

        Host.Children.Add(BuildListCard(148,
            new RowSpec(68, 44, 84, 70), new RowSpec(68, 44, 70, 58)));

        Host.Children.Add(BuildListCard(176,
            new RowSpec(85, 40, 66, 74), new RowSpec(85, 40, 54, 62)));

        Host.Children.Add(BuildListCard(130,
            new RowSpec(107, 44, 60, 78), new RowSpec(107, 44, 52, 66)));

        Host.Children.Add(BuildListCard(110,
            new RowSpec(70, 38, 116, 0), new RowSpec(70, 38, 148, 0)));

        Host.Children.Add(BuildListCard(148,
            new RowSpec(107, 34, 96, 62), new RowSpec(107, 34, 78, 54)));
    }

    /// <summary>
    /// The hero is the page's HeroCardStyle at padding 22: a title row, the
    /// headline figure, then the range and confidence row.
    /// </summary>
    private static View BuildHero()
    {
        var titleRow = SkeletonShapes.HStack(10,
            SkeletonShapes.Circle(44, SkeletonPalette.OnDark),
            SkeletonShapes.Filling(0,
                Bar(190, 16, width: 152, fill: SkeletonPalette.OnDark),
                Bar(190, 12, width: 122, fill: SkeletonPalette.OnDark)));

        var headline = VStack(2,
            Bar(190, 12, width: 118, fill: SkeletonPalette.OnDark),
            Bar(190, 28, width: 146, fill: SkeletonPalette.OnDark));

        var range = VStack(2,
            Bar(190, 12, width: 38, fill: SkeletonPalette.OnDark),
            Bar(190, 13, width: 92, fill: SkeletonPalette.OnDark));

        var confidence = VStack(2,
            Bar(190, 12, width: 66, fill: SkeletonPalette.OnDark),
            Slab(20, width: 58, fill: SkeletonPalette.OnDark, radius: 12));
        confidence.HorizontalOptions = LayoutOptions.End;

        var footers = SkeletonShapes.HStack(14, range, SkeletonShapes.Filler(), confidence);

        var body = VStack(12, titleRow, headline, footers);

        return new Border
        {
            BackgroundColor = FinanceApp.Mobile.Helpers.FinoraOverlay.Resolve("FinoraInkSoft", "#232923"),
            Stroke = new SolidColorBrush(SkeletonPalette.Ink),
            StrokeThickness = 1.5,
            Padding = new Thickness(22),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28) },
            Content = body
        };
    }

    /// <summary>
    /// One of the page's five section cards: an 18pt heading, then rows of the
    /// given height. Row heights differ per section because each uses a different
    /// frame style.
    /// </summary>
private static View BuildListCard(double headingWidth, params RowSpec[] rows)
    {
        var heading = Bar(190, 18, width: headingWidth);
        heading.HorizontalOptions = LayoutOptions.Start;

        var list = VStack(8);

        foreach (var (rowHeight, icon, lead, trail) in rows)
        {
            View content;
            if (trail > 0)
            {
                var details = SkeletonShapes.Filling(4,
                    Bar(190, 14, width: lead),
                    Bar(190, 11, width: lead * 0.7));

                var trailing = SkeletonShapes.Trailing(4,
                    Bar(190, 14, width: trail),
                    Bar(190, 11, width: trail * 0.7));

                content = SkeletonShapes.HStack(12, SkeletonShapes.IconDisc((int)icon), details, trailing);
            }
            else
            {
                // Insights are text-only: title, message, and no trailing column.
                var withCaption = SkeletonShapes.Filling(2,
                    Bar(190, 14, width: lead),
                    Bar(190, 13, width: lead * 1.35),
                    Bar(190, 11, width: lead * 0.8));

                content = SkeletonShapes.HStack(12, SkeletonShapes.IconDisc((int)icon), withCaption);
            }

            list.Add(SkeletonShapes.Row((int)rowHeight, 20, content, padding: 12));
        }

        var body = VStack(12, heading, list);

        return new Border
        {
            BackgroundColor = SkeletonPalette.Surface,
            Stroke = new SolidColorBrush(SkeletonPalette.Ink),
            StrokeThickness = 1.5,
            Padding = new Thickness(16),
            Margin = new Thickness(0, 4, 0, 0),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28) },
            Content = body
        };
    }
}