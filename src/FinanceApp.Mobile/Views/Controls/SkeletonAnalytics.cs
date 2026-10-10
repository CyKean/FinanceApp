namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;
using static FinanceApp.Mobile.Views.Controls.SkeletonShapes;

/// <summary>
/// Analytics: header, the Today/Weekly/Monthly/Yearly pills, the Earning and
/// Spending tiles, the Overview chart card, and the Income vs Expenses card.
/// <para>
/// This was the least accurate placeholder in the app: it led with a row of four
/// stat tiles, then a donut chart and a savings gauge - none of which exist on
/// this page. It also omitted the period pills, the header, and the closing
/// Income vs Expenses card. Three invented sections, three missing ones.
/// </para>
/// </summary>
public class SkeletonAnalytics : SkeletonPage
{
    public SkeletonAnalytics()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 24) })
    {
    }

    protected override void Populate()
    {
        Host.Children.Add(SkeletonShapes.PageHeader(
            trailing: SkeletonShapes.Circle(46, SkeletonPalette.Surface),
            titleWidth: 96,
            subtitleWidth: 0));

        Host.Children.Add(BuildPeriodPills());

        Host.Children.Add(BuildEarningSpendingPair());

        Host.Children.Add(BuildOverviewCard());

        Host.Children.Add(BuildIncomeVsExpensesCard());
    }

    /// <summary>Four 12pt captions in pills, 39 tall including their padding.</summary>
    private static View BuildPeriodPills()
    {
        var grid = new Grid { ColumnSpacing = 8 };

        var columns = new ColumnDefinitionCollection();
        for (var i = 0; i < 4; i++)
            columns.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions = columns;

        var widths = new[] { 40d, 52d, 58d, 48d };

        for (var i = 0; i < 4; i++)
        {
            var label = Bar(190, 12, width: widths[i]);
            label.HorizontalOptions = LayoutOptions.Center;
            label.VerticalOptions = LayoutOptions.Center;

            var pill = new Border
            {
                Padding = new Thickness(0, 12),
                BackgroundColor = SkeletonPalette.Surface,
                Stroke = new SolidColorBrush(SkeletonPalette.Ink),
                StrokeThickness = 1.5,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(100) },
                Content = label
            };

            Grid.SetColumn(pill, i);
            grid.Add(pill);
        }

        return grid;
    }

    /// <summary>
    /// The lime Earning tile beside the dark Spending tile. The dark one carries
    /// a 130pt radar chart, which is what sets the row's height.
    /// </summary>
    private static View BuildEarningSpendingPair()
    {
        var grid = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) }
        };

        grid.Add(BuildEarningTile());

        var spending = BuildSpendingTile();
        Grid.SetColumn(spending, 1);
        grid.Add(spending);

        return grid;
    }

    private static View BuildEarningTile()
    {
        var heading = SkeletonShapes.HStack(6,
            Bar(190, 12, width: 54, fill: SkeletonPalette.Ink),
            SkeletonShapes.Filler(),
            Circle(26, SkeletonPalette.Ink));

        var copy = VStack(4,
            heading,
            Bar(190, 28, width: 118, fill: SkeletonPalette.Ink),
            Bar(190, 10, width: 152, fill: SkeletonPalette.Ink),
            Bar(190, 10, width: 118, fill: SkeletonPalette.Ink));

        return TintedCard(173, SkeletonPalette.Lime, copy);
    }

    private static View BuildSpendingTile()
    {
        var heading = SkeletonShapes.HStack(6,
            Bar(190, 12, width: 62, fill: SkeletonPalette.OnDark),
            SkeletonShapes.Filler(),
            Circle(26, SkeletonPalette.Lime));

        var chart = Slab(130, fill: SkeletonPalette.InkSoft, radius: 12);
        chart.HorizontalOptions = LayoutOptions.Fill;

        var caption = Bar(190, 9, width: 128, fill: SkeletonPalette.OnDark);
        caption.HorizontalOptions = LayoutOptions.Start;

        var copy = VStack(4,
            heading,
            Bar(190, 20, width: 104, fill: SkeletonPalette.OnDark),
            chart,
            caption);

        return TintedCard(209, SkeletonPalette.Ink, copy);
    }

    private static View TintedCard(double height, Color background, View content) => new Border
    {
        BackgroundColor = background,
        Stroke = new SolidColorBrush(SkeletonPalette.Ink),
        StrokeThickness = 1.5,
        Padding = new Thickness(18),
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28) },
        Content = new Border
        {
            HeightRequest = height,
            Padding = 0,
            BackgroundColor = Colors.Transparent,
            StrokeThickness = 0,
            Content = content
        }
    };

    /// <summary>Title, the total-balance block, and the chart body.</summary>
    private static View BuildOverviewCard()
    {
        var title = Bar(190, 16, width: 86);
        title.HorizontalOptions = LayoutOptions.Start;

        var legend = VStack(2,
            HStack(5, Slab(8, width: 8, fill: SkeletonPalette.Lime, radius: 2), Bar(190, 10, width: 48)),
            HStack(5, Slab(8, width: 8, fill: SkeletonPalette.Ink, radius: 2), Bar(190, 10, width: 54)));
        legend.VerticalOptions = LayoutOptions.Center;

        var balance = VStack(0,
            Bar(190, 11, width: 72),
            Bar(190, 22, width: 104));

        var summary = SkeletonShapes.HStack(0, balance, legend);

        var chart = Slab(190, fill: SkeletonPalette.Block, radius: 8);
        chart.HorizontalOptions = LayoutOptions.Fill;

        var body = VStack(10, title, summary, chart);

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

    /// <summary>The closing card: two small tiles over the net row.</summary>
    private static View BuildIncomeVsExpensesCard()
    {
        var title = Bar(190, 15, width: 128);
        title.HorizontalOptions = LayoutOptions.Start;

        var income = SkeletonShapes.ContrastCard(
            45, 18, SkeletonPalette.Lime, SkeletonPalette.Ink,
            Bar(190, 11, width: 48),
            Bar(190, 15, width: 66));

        var expense = SkeletonShapes.ContrastCard(
            45, 18, SkeletonPalette.Ink, SkeletonPalette.OnDark,
            Bar(190, 11, width: 54),
            Bar(190, 15, width: 60));
        Grid.SetColumn(expense, 1);

        var tiles = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) }
        };
        tiles.Add(income);
        tiles.Add(expense);

        var net = HStack(8, Bar(190, 12, width: 30), Bar(190, 13, width: 74));

        var body = VStack(8, title, tiles, net);

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