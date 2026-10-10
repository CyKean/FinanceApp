namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;
using static FinanceApp.Mobile.Views.Controls.SkeletonShapes;

/// <summary>
/// Dashboard: header, the balance hero, quick actions, the expense/income pair,
/// recent activity, the savings/budgets pair, and the two closing cards.
/// <para>
/// The gaps here were the widest of all: the hero was a generic dark slab rather
/// than the page's 34-radius card with its five lines and contactless bar, the
/// buttons were 8 short, recent activity was drawn as four loose cards instead
/// of one 268pt list inside a card, "Spending by Category" showed a donut the
/// page does not have, and the Goals card was missing outright.
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
        Host.Children.Add(SkeletonShapes.BalanceHero());
        Host.Children.Add(BuildQuickActions());
        Host.Children.Add(BuildPrimaryButtons());
        Host.Children.Add(BuildRecentActivity());
        Host.Children.Add(BuildSplitCards());
        Host.Children.Add(BuildCategoryCard());
        Host.Children.Add(BuildGoalsCard());
    }

    private static View BuildHeader()
    {
        var text = SkeletonShapes.Filling(0,
            SkeletonShapes.Bar(190, 12, width: 104),
            SkeletonShapes.Bar(190, 18, width: 132));

        return SkeletonShapes.HStack(10,
            SkeletonShapes.Circle(48, SkeletonPalette.Block),
            text,
            SkeletonShapes.Filler(),
            SkeletonShapes.Circle(46, SkeletonPalette.Surface));
    }

    /// <summary>
    /// Four 64pt circles with a caption under each. This used to be five bare
    /// 44pt circles, so the row was both the wrong count and a caption shorter.
    /// </summary>
    private static View BuildQuickActions()
    {
        var grid = new Grid { ColumnSpacing = 10 };

        var columns = new ColumnDefinitionCollection();
        for (var i = 0; i < 4; i++)
            columns.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions = columns;

        var captions = new[] { 62d, 36d, 58d, 40d };

        for (var i = 0; i < 4; i++)
        {
            var disc = SkeletonShapes.Circle(64, SkeletonPalette.Surface);
            disc.HorizontalOptions = LayoutOptions.Center;

            var caption = SkeletonShapes.Bar(190, 12, width: captions[i]);
            caption.HorizontalOptions = LayoutOptions.Center;

            var column = SkeletonShapes.VStack(6, disc, caption);

            Grid.SetColumn(column, i);
            grid.Add(column);
        }

        return grid;
    }

    /// <summary>The page's two buttons: 56 for expense, 52 for income.</summary>
    private static View BuildPrimaryButtons()
    {
        var grid = new Grid
        {
            ColumnSpacing = 12,
            HeightRequest = 56,
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) }
        };

        grid.Add(SkeletonShapes.ActionButton(56, 28, SkeletonPalette.Ink));

        var income = SkeletonShapes.ActionButton(52, 26, SkeletonPalette.Lime);
        income.VerticalOptions = LayoutOptions.Center;
        Grid.SetColumn(income, 1);
        grid.Add(income);

        return grid;
    }

    /// <summary>
    /// A heading row, then the single card the page wraps its 268pt list in -
    /// not four loose cards, which is what this used to draw.
    /// </summary>
    private static View BuildRecentActivity()
    {
        var title = Bar(190, 16, width: 128);
        title.VerticalOptions = LayoutOptions.Center;

        var seeAll = Bar(190, 13, width: 56);
        seeAll.VerticalOptions = LayoutOptions.Center;
        seeAll.HorizontalOptions = LayoutOptions.End;

        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) }
        };
        heading.Add(title);
        Grid.SetColumn(seeAll, 1);
        heading.Add(seeAll);

        var rows = VStack(0);
        for (var i = 0; i < 3; i++)
        {
            rows.Add(SkeletonShapes.HStack(12,
                SkeletonShapes.IconDisc(46),
                SkeletonShapes.Filling(1, Bar(190, 14, width: i % 2 == 0 ? 92 : 72), Bar(190, 11, width: 118)),
                Bar(190, 14, width: 84)));
        }

        var list = new Border
        {
            BackgroundColor = SkeletonPalette.Surface,
            Stroke = new SolidColorBrush(SkeletonPalette.Ink),
            StrokeThickness = 1.5,
            Padding = new Thickness(8),
            HeightRequest = 268,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28) },
            Content = rows
        };

        return SkeletonShapes.VStack(10, heading, list);
    }

    /// <summary>The lime savings tile beside the dark budgets tile.</summary>
    private static View BuildSplitCards()
    {
        var grid = new Grid
        {
            ColumnSpacing = 12,
            HeightRequest = 109,
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) }
        };

        var savings = SkeletonShapes.ContrastCard(
            109, 28, SkeletonPalette.Lime, SkeletonPalette.Ink,
            Bar(190, 12, width: 72),
            Bar(190, 26, width: 84),
            Bar(190, 11, width: 92));
        grid.Add(savings);

        var budgets = SkeletonShapes.ContrastCard(
            109, 28, SkeletonPalette.Ink, SkeletonPalette.OnDark,
            Bar(190, 12, width: 78),
            Bar(190, 20, width: 68),
            Bar(190, 11, width: 118));
        Grid.SetColumn(budgets, 1);
        grid.Add(budgets);

        return grid;
    }

    /// <summary>
    /// "Spending by Category" is a 170pt list of icon/name/bar/amount rows on the
    /// page. The old placeholder drew a 128pt donut here, which the page has
    /// never contained.
    /// </summary>
    private static View BuildCategoryCard() => BuildSummaryCard(
        headingWidth: 152,
        listHeight: 170,
        iconSize: 40,
        rowHeight: 44,
        withProgress: true);

    /// <summary>The Goals card, which the old placeholder left out entirely.</summary>
    private static View BuildGoalsCard() => BuildSummaryCard(
        headingWidth: 56,
        listHeight: 150,
        iconSize: 38,
        rowHeight: 44,
        withProgress: true);

    private static View BuildSummaryCard(
        double headingWidth,
        double listHeight,
        double iconSize,
        double rowHeight,
        bool withProgress)
    {
        var title = Bar(190, 15, width: headingWidth);
        title.VerticalOptions = LayoutOptions.Center;

        var chevron = Bar(190, 16, width: 9);
        chevron.VerticalOptions = LayoutOptions.Center;

        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) }
        };
        heading.Add(title);
        Grid.SetColumn(chevron, 1);
        heading.Add(chevron);

        var rows = VStack(0);
        for (var i = 0; i < 3; i++)
        {
            var details = withProgress
                ? SkeletonShapes.Filling(4, Bar(190, 13, width: 82 - (i % 2) * 22), Bar(190, 6, width: 124))
                : SkeletonShapes.Filling(4, Bar(190, 13, width: 82 - (i % 2) * 22));

            var trailing = Bar(190, 13, width: 76);
            trailing.VerticalOptions = LayoutOptions.Center;

            var row = SkeletonShapes.HStack(10, SkeletonShapes.IconDisc((int)iconSize), details, trailing);
            row.HeightRequest = rowHeight;

            rows.Add(row);
        }

        var list = new Border
        {
            BackgroundColor = SkeletonPalette.Surface,
            Padding = new Thickness(4, 8),
            HeightRequest = listHeight,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20) },
            Content = rows
        };

        var body = VStack(10, heading, list);

        return new Border
        {
            BackgroundColor = SkeletonPalette.Surface,
            Stroke = new SolidColorBrush(SkeletonPalette.Ink),
            StrokeThickness = 1.5,
            Padding = new Thickness(20),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28) },
            Content = body
        };
    }
}