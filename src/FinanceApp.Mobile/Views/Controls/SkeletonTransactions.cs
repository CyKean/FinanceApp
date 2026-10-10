namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;

/// <summary>
/// Transactions: a section-for-section mirror of the page - header, filter row,
/// the income/expense pair, the net pill, then transaction rows.
/// <para>
/// This is the page that made the problem obvious. The placeholder used to open
/// on the summary cards, so everything above them (the header, the filter row)
/// and the net pill below them appeared for the first time when data landed, and
/// the rows themselves were a generic shape: no header, no net pill, a phantom
/// section heading, and a pale circle where the page draws a filled icon disc.
/// Every dimension below is taken from the page's own XAML and the row/label
/// styles it uses.
/// </para>
/// </summary>
public class SkeletonTransactions : SkeletonPage
{
    /// <summary>
    /// Header, filter row and rows sit in a CollectionView header that scrolls
    /// under the floating tab bar, so the rows carry the bottom clearance
    /// themselves (see the footer ActivityIndicator's 96 margin on the page).
    /// </summary>
    private static readonly Thickness s_pagePadding = new(18, 14, 18, 96);

    public SkeletonTransactions()
        : base(new VerticalStackLayout { Spacing = 12, Padding = s_pagePadding })
    {
    }

    protected override void Populate()
    {
        // 46 + "Transactions" 18 bold / "History of money in and out" 12, with the
        // page's ink-filled circle carrying the "+" on the trailing edge.
        Host.Children.Add(SkeletonShapes.PageHeader(
            trailing: SkeletonShapes.Circle(46, SkeletonPalette.Ink),
            titleWidth: 124,
            subtitleWidth: 158));

        Host.Children.Add(SkeletonShapes.FilterBarRow());

        Host.Children.Add(BuildSummaryStrip());

        Host.Children.Add(BuildNetPill());

        var rows = SkeletonShapes.VStack(8);
        for (var i = 0; i < 6; i++)
        {
            rows.Add(BuildRow(i));
        }

        Host.Children.Add(rows);
    }

    /// <summary>
    /// The lime income tile beside the dark expense tile. Both invert the page's
    /// normal card palette, so their placeholders are painted in the on-card
    /// colour - the default block colour is invisible against either.
    /// </summary>
    private static View BuildSummaryStrip()
    {
        var grid = new Grid
        {
            ColumnSpacing = 12,
            HeightRequest = 64,
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) }
        };

        var income = SkeletonShapes.ContrastCard(
            64, 28, SkeletonPalette.Lime, SkeletonPalette.Ink,
            SkeletonShapes.Bar(190, 11, width: 62),
            SkeletonShapes.Bar(190, 17, width: 74));

        grid.Add(income);

        var expense = SkeletonShapes.ContrastCard(
            64, 28, SkeletonPalette.Ink, SkeletonPalette.OnDark,
            SkeletonShapes.Bar(190, 11, width: 70),
            SkeletonShapes.Bar(190, 17, width: 82));
        Grid.SetColumn(expense, 1);
        grid.Add(expense);

        return grid;
    }

    /// <summary>The full-width "Net -x • n txns" pill that closes the summary.</summary>
    private static View BuildNetPill()
    {
        var content = SkeletonShapes.HStack(8,
            SkeletonShapes.Bar(190, 12, width: 30),
            SkeletonShapes.Bar(190, 14, width: 82),
            SkeletonShapes.Circle(4),
            SkeletonShapes.Bar(190, 12, width: 54));
        content.HorizontalOptions = LayoutOptions.Center;
        content.VerticalOptions = LayoutOptions.Center;

        var pill = new Border
        {
            BackgroundColor = SkeletonPalette.Surface,
            Stroke = new SolidColorBrush(SkeletonPalette.Block),
            StrokeThickness = 1,
            Padding = new Thickness(14, 10),
            HeightRequest = 42,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(100) },
            Content = content
        };

        return pill;
    }

    /// <summary>
    /// One transaction row: a 48 icon disc, the category name, an optional notes
    /// line, the account-and-date line, and a trailing amount over its sync mark.
    /// The row is 48 of content inside 12 of padding, so 72 - which is what the
    /// page actually measures, and what the old 68 was short of.
    /// </summary>
    private static View BuildRow(int index)
    {
        var meta = SkeletonShapes.HStack(6,
            SkeletonShapes.Bar(190, 11, width: index % 3 == 1 ? 34 : 46),
            SkeletonShapes.Circle(3),
            SkeletonShapes.Bar(190, 11, width: index % 2 == 0 ? 84 : 72));

        // The page's detail column runs CategoryName / Notes / account-and-date at
        // spacing 2, and the trailing column runs amount over its sync mark the
        // same way.
        var details = SkeletonShapes.Filling(2,
            SkeletonShapes.Bar(190, 14, width: index % 3 == 2 ? 74 : 92),
            SkeletonShapes.Bar(190, 12, width: index % 2 == 0 ? 108 : 86),
            meta);

        var trailing = SkeletonShapes.Trailing(2,
            SkeletonShapes.Bar(190, 14, width: 88),
            SkeletonShapes.Circle(10));

        var content = SkeletonShapes.HStack(12, SkeletonShapes.IconDisc(48), details, trailing);

        return SkeletonShapes.Row(72, 22, content, padding: 12);
    }
}