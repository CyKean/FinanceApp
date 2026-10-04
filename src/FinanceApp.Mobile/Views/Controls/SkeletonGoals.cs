namespace FinanceApp.Mobile.Views.Controls;

/// <summary>
/// Goals: like budgets, each goal carries a saved-versus-target progress bar,
/// but taller still and with a status pill on the trailing edge.
/// </summary>
public class SkeletonGoals : SkeletonPage
{
    public SkeletonGoals()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 100) })
    {
    }

    protected override void Populate()
    {
        for (var i = 0; i < 4; i++)
        {
            Host.Children.Add(BuildCard(i));
        }
    }

    private static View BuildCard(int index)
    {
        var header = SkeletonShapes.HStack(10,
            SkeletonShapes.Circle(34),
            SkeletonShapes.Filling(
                SkeletonShapes.Bar(190, 13, width: index % 3 == 2 ? 112 : 152),
                SkeletonShapes.Bar(190, 10, width: index % 2 == 0 ? 82 : 64)),
            // Status pill: a small rounded bar rather than a value column.
            SkeletonShapes.Bar(190, 18, width: 62));

        var fill = index % 3 == 0 ? 0.45 : index % 3 == 1 ? 0.72 : 0.28;

        var footer = SkeletonShapes.HStack(8,
            SkeletonShapes.Filling(SkeletonShapes.ProgressBar(190, fill)),
            SkeletonShapes.Bar(190, 10, width: 64));

        return SkeletonShapes.Row(112, 18, SkeletonShapes.VStack(12, header, footer));
    }
}