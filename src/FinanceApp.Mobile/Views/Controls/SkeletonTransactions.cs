namespace FinanceApp.Mobile.Views.Controls;

/// <summary>
/// Transactions: the summary strip the page renders above the list, then rows
/// that carry a category initial, a title, a date and a trailing amount.
/// </summary>
public class SkeletonTransactions : SkeletonPage
{
    public SkeletonTransactions()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 24) })
    {
    }

    protected override void Populate()
    {
        Host.Children.Add(BuildSummaryStrip());
        Host.Children.Add(SectionHeading(96));

        for (var i = 0; i < 6; i++)
        {
            Host.Children.Add(BuildRow(i));
        }
    }

    /// <summary>Two cards side by side, matching the page's summary grid.</summary>
    private static View BuildSummaryStrip()
    {
        var grid = new Grid
        {
            ColumnSpacing = 12,
            HeightRequest = 76,
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) }
        };

        grid.Add(BuildSummaryCard("Income", 96, 62));
        var expense = BuildSummaryCard("Expense", 96, 74);
        Grid.SetColumn(expense, 1);
        grid.Add(expense);

        return grid;
    }

    private static View BuildSummaryCard(string? label, double labelWidth, double valueWidth)
    {
        var card = SkeletonShapes.Card(76, radius: 16);
        card.Content = SkeletonShapes.VStack(8,
            SkeletonShapes.Bar(190, 10, width: labelWidth),
            SkeletonShapes.Bar(190, 18, width: valueWidth));

        return card;
    }

    private static View BuildRow(int index)
    {
        var content = SkeletonShapes.HStack(12,
            SkeletonShapes.Circle(38),
            SkeletonShapes.Filling(
                SkeletonShapes.Bar(190, 12, width: index % 3 == 1 ? 128 : 158),
                SkeletonShapes.Bar(190, 10, width: index % 2 == 0 ? 92 : 68)),
            SkeletonShapes.Trailing(
                SkeletonShapes.Bar(190, 12, width: 74),
                SkeletonShapes.Bar(190, 9, width: 48)));

        return SkeletonShapes.Row(68, 16, content);
    }
}