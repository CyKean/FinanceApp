namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;

/// <summary>
/// Dashboard: a section-for-section mirror of the landing page - header, dark
/// balance hero, circular quick actions, the two primary buttons, recent activity
/// rows, the savings/budgets pair, then the spending-by-category donut.
/// <para>
/// This is the page a user sees most and the one a generic row placeholder
/// rearranged most badly, which is why it gets its own shape rather than a
/// parameterised one.
/// </para>
/// </summary>
public class SkeletonDashboard : SkeletonPage
{
    public SkeletonDashboard()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 100) })
    {
    }

    protected override void Populate()
    {
        Host.Children.Add(BuildHeader());
        Host.Children.Add(BuildHeroCard());
        Host.Children.Add(BuildQuickActions());
        Host.Children.Add(BuildPrimaryButtons());
        Host.Children.Add(BuildRecentHeading());
        Host.Children.Add(BuildRecentRows());
        Host.Children.Add(BuildSplitCards());
        Host.Children.Add(BuildCategoryCard());
    }

    private static View BuildHeader()
    {
        var text = SkeletonShapes.Centered(
            SkeletonShapes.Bar(190, 13, width: 120),
            SkeletonShapes.Bar(190, 10, width: 92));

        // Filler pushes the bell placeholder to the trailing edge.
        return SkeletonShapes.HStack(10, SkeletonShapes.Circle(48), text, SkeletonShapes.Filler(), SkeletonShapes.Circle(34));
    }

    private static View BuildHeroCard() => SkeletonShapes.DarkCard(168, 24,
        SkeletonShapes.Bar(190, 11, width: 54, fill: SkeletonPalette.Ink),
        SkeletonShapes.Bar(190, 26, width: 178, fill: SkeletonPalette.Ink),
        SkeletonShapes.Bar(190, 12, width: 132, fill: SkeletonPalette.Ink));

    private static View BuildQuickActions() => SkeletonShapes.HStack(10,
        SkeletonShapes.CircleButton(), SkeletonShapes.CircleButton(), SkeletonShapes.CircleButton(),
        SkeletonShapes.CircleButton(), SkeletonShapes.CircleButton());

    private static View BuildPrimaryButtons()
    {
        var row = new Grid
        {
            ColumnSpacing = 12,
            HeightRequest = 48,
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) }
        };

        row.Add(SkeletonShapes.Slab(48, radius: 14));

        var second = SkeletonShapes.Slab(48, radius: 14);
        Grid.SetColumn(second, 1);
        row.Add(second);

        return row;
    }

    private static View BuildRecentHeading()
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) }
        };

        var title = SkeletonShapes.Bar(190, 16, width: 128);
        title.VerticalOptions = LayoutOptions.Center;
        row.Add(title);

        var seeAll = SkeletonShapes.Bar(190, 13, width: 54);
        seeAll.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(seeAll, 1);
        row.Add(seeAll);

        return row;
    }

    private static View BuildRecentRows()
    {
        var stack = SkeletonShapes.VStack(10);

        for (var i = 0; i < 4; i++)
        {
            var content = SkeletonShapes.HStack(12,
                SkeletonShapes.Circle(34),
                SkeletonShapes.Filling(
                    SkeletonShapes.Bar(190, 12, width: i % 2 == 0 ? 142 : 108),
                    SkeletonShapes.Bar(190, 10, width: i % 3 == 0 ? 118 : 88)),
                SkeletonShapes.Trailing(
                    SkeletonShapes.Bar(190, 11, width: 70),
                    SkeletonShapes.Bar(190, 9, width: 52)));

            stack.Add(SkeletonShapes.Row(60, 16, content));
        }

        return stack;
    }

    private static View BuildSplitCards()
    {
        var row = new Grid
        {
            ColumnSpacing = 12,
            HeightRequest = 108,
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) }
        };

        var savings = SkeletonShapes.Card(108, radius: 18);
        savings.Content = SkeletonShapes.VStack(8,
            SkeletonShapes.Bar(190, 11, width: 58),
            SkeletonShapes.Bar(190, 24, width: 84),
            SkeletonShapes.Bar(190, 10, width: 72));
        row.Add(savings);

        var budgets = SkeletonShapes.Slab(108, radius: 18);
        Grid.SetColumn(budgets, 1);
        row.Add(budgets);

        return row;
    }

    private static View BuildCategoryCard()
    {
        var card = SkeletonShapes.Card(196, radius: 20);

        var body = SkeletonShapes.HStack(16,
            SkeletonShapes.Circle(128),
            SkeletonShapes.Filling(
                SkeletonShapes.HStack(8, SkeletonShapes.Circle(10), SkeletonShapes.Bar(190, 10, width: 62)),
                SkeletonShapes.HStack(8, SkeletonShapes.Circle(10), SkeletonShapes.Bar(190, 10, width: 48)),
                SkeletonShapes.HStack(8, SkeletonShapes.Circle(10), SkeletonShapes.Bar(190, 10, width: 70))));

        card.Content = SkeletonShapes.VStack(12, SkeletonShapes.Bar(190, 14, width: 156), body);
        return card;
    }
}