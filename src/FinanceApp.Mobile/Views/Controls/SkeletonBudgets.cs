namespace FinanceApp.Mobile.Views.Controls;

/// <summary>
/// Budgets: taller cards than the other lists, because each row is a budget with
/// an amount, a spend figure and a progress bar - the bar being the one thing
/// whose absence makes the layout jump.
/// </summary>
public class SkeletonBudgets : SkeletonPage
{
    public SkeletonBudgets()
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
                SkeletonShapes.Bar(190, 13, width: index % 2 == 0 ? 148 : 116),
                SkeletonShapes.Bar(190, 10, width: index % 3 == 1 ? 68 : 92)),
            SkeletonShapes.Trailing(
                SkeletonShapes.Bar(190, 14, width: 76),
                SkeletonShapes.Bar(190, 9, width: 44)));

        // Two thirds, then a third: a partly filled bar reads as a real gauge,
        // whereas a full bar would just look like a solid rule.
        var fill = index % 3 == 0 ? 0.62 : index % 3 == 1 ? 0.38 : 0.8;

        var footer = SkeletonShapes.HStack(8,
            SkeletonShapes.Filling(SkeletonShapes.ProgressBar(190, fill)),
            SkeletonShapes.Bar(190, 10, width: 54));

        var content = SkeletonShapes.VStack(12, header, footer);
        return SkeletonShapes.Row(104, 18, content);
    }
}