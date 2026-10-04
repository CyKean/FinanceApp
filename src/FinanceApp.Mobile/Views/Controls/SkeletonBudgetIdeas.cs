namespace FinanceApp.Mobile.Views.Controls;

/// <summary>
/// Budget Ideas: a stack of standalone suggestion cards, each with a heading, a
/// prose line or two and a suggested amount. No list rows, no tiles.
/// </summary>
public class SkeletonBudgetIdeas : SkeletonPage
{
    public SkeletonBudgetIdeas()
        : base(new VerticalStackLayout { Spacing = 16, Padding = new Thickness(20, 16, 20, 28) })
    {
    }

    protected override void Populate()
    {
        for (var i = 0; i < 4; i++)
        {
            Host.Children.Add(BuildSuggestion(i));
        }
    }

    private static View BuildSuggestion(int index)
    {
        var heading = SkeletonShapes.HStack(10,
            SkeletonShapes.Circle(32),
            SkeletonShapes.Bar(190, 13, width: index % 2 == 0 ? 168 : 124));

        var body = SkeletonShapes.VStack(8,
            SkeletonShapes.Bar(190, 10, width: index % 3 == 0 ? 186 : 156),
            SkeletonShapes.Bar(190, 10, width: index % 2 == 0 ? 142 : 172));

        var amount = SkeletonShapes.HStack(10,
            SkeletonShapes.Slab(38, width: 108, radius: 12),
            SkeletonShapes.Bar(190, 13, width: 72));

        var card = SkeletonShapes.Card(148, radius: 20);
        card.Content = SkeletonShapes.VStack(12, heading, body, amount);
        return card;
    }
}