namespace FinanceApp.Mobile.Views.Controls;

/// <summary>
/// Forecasts: the page opens with a dark prediction hero, then the spending-trend
/// chart, then a list of budget-forecast rows and a list of insights - so the
/// placeholder has to move from hero, to chart, to list.
/// </summary>
public class SkeletonForecasts : SkeletonPage
{
    public SkeletonForecasts()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 24) })
    {
    }

    protected override void Populate()
    {
        Host.Children.Add(BuildHero());

        Host.Children.Add(SectionHeading(150));
        Host.Children.Add(ChartCard(200, titleWidth: 150, legend: true, bodyHeight: 104));

        Host.Children.Add(SectionHeading(168));
        for (var i = 0; i < 3; i++)
        {
            Host.Children.Add(BuildForecastRow(i));
        }

        Host.Children.Add(SectionHeading(104));
        for (var i = 0; i < 3; i++)
        {
            Host.Children.Add(BuildInsightRow(i));
        }
    }

    private static View BuildHero()
    {
        return SkeletonShapes.DarkCard(168, 24,
            SkeletonShapes.Bar(190, 11, width: 54, fill: SkeletonPalette.Ink),
            SkeletonShapes.Bar(190, 26, width: 178, fill: SkeletonPalette.Ink),
            SkeletonShapes.Bar(190, 12, width: 132, fill: SkeletonPalette.Ink));
    }

    /// <summary>A budget row: name, predicted amount and a range underneath.</summary>
    private static View BuildForecastRow(int index)
    {
        var content = SkeletonShapes.HStack(12,
            SkeletonShapes.Circle(34),
            SkeletonShapes.Filling(
                SkeletonShapes.Bar(190, 12, width: index % 2 == 0 ? 132 : 104),
                SkeletonShapes.Bar(190, 10, width: index % 3 == 0 ? 112 : 84)),
            SkeletonShapes.Trailing(
                SkeletonShapes.Bar(190, 13, width: 70),
                SkeletonShapes.Bar(190, 9, width: 58)));

        return SkeletonShapes.Row(76, 16, content);
    }

    /// <summary>An insight is text-only, with a severity dot on the leading edge.</summary>
    private static View BuildInsightRow(int index)
    {
        var content = SkeletonShapes.HStack(10,
            SkeletonShapes.Circle(10),
            SkeletonShapes.Filling(
                SkeletonShapes.Bar(190, 12, width: index % 3 == 2 ? 148 : 186),
                SkeletonShapes.Bar(190, 10, width: index % 2 == 0 ? 128 : 164)));

        return SkeletonShapes.Row(64, 16, content);
    }
}