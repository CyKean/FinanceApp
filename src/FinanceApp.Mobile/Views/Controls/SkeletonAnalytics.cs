namespace FinanceApp.Mobile.Views.Controls;

/// <summary>
/// Analytics: four stat tiles across the top, then the spending donut, the
/// monthly trend chart and the savings gauge - in the order the page stacks them.
/// </summary>
public class SkeletonAnalytics : SkeletonPage
{
    public SkeletonAnalytics()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 24) })
    {
    }

    protected override void Populate()
    {
        Host.Children.Add(StatTiles(4, height: 96));

        Host.Children.Add(SectionHeading(150));
        Host.Children.Add(BuildDonut());

        Host.Children.Add(SectionHeading(138));
        Host.Children.Add(ChartCard(190, titleWidth: 138, legend: false, bodyHeight: 96));

        Host.Children.Add(BuildGauge());
    }

    private static View BuildDonut()
    {
        var card = SkeletonShapes.Card(230, radius: 20);

        // Donut on the left, legend rows on the right.
        var body = SkeletonShapes.HStack(16,
            SkeletonShapes.Circle(132),
            SkeletonShapes.Filling(
                SkeletonShapes.HStack(8, SkeletonShapes.Circle(10), SkeletonShapes.Bar(190, 10, width: 76)),
                SkeletonShapes.HStack(8, SkeletonShapes.Circle(10), SkeletonShapes.Bar(190, 10, width: 62)),
                SkeletonShapes.HStack(8, SkeletonShapes.Circle(10), SkeletonShapes.Bar(190, 10, width: 84)),
                SkeletonShapes.HStack(8, SkeletonShapes.Circle(10), SkeletonShapes.Bar(190, 10, width: 54))));

        card.Content = SkeletonShapes.VStack(12, SkeletonShapes.Bar(190, 14, width: 156), body);
        return card;
    }

    private static View BuildGauge()
    {
        var card = SkeletonShapes.Card(150, radius: 20);
        card.Content = SkeletonShapes.VStack(10,
            SkeletonShapes.Bar(190, 14, width: 122),
            SkeletonShapes.Circle(104));

        return card;
    }
}