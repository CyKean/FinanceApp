namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;

/// <summary>
/// Calendar: month header, weekday strip, a real seven-column grid of day cells,
/// then the selected day's event rows.
/// <para>
/// The grid is built at the real aspect rather than as a stack of bars, because
/// the page builds its month in code the same way - a placeholder of the wrong
/// shape here would resize more than anywhere else in the app.
/// </para>
/// </summary>
public class SkeletonCalendar : SkeletonPage
{
    private const int Columns = 7;
    private const int Weeks = 6;
    private const double CellHeight = 46;

    public SkeletonCalendar()
        : base(new VerticalStackLayout { Spacing = 12, Padding = new Thickness(18, 14, 18, 24) })
    {
    }

    protected override void Populate()
    {
        Host.Children.Add(BuildMonthHeader());
        Host.Children.Add(BuildWeekdayStrip());
        Host.Children.Add(BuildGrid());
        Host.Children.Add(SectionHeading(132));
        Host.Children.Add(SkeletonShapes.VStack(10, BuildEventRow(), BuildEventRow()));
    }

    private static View BuildMonthHeader()
    {
        var grid = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Star),
                new(GridLength.Auto)
            }
        };

        var back = SkeletonShapes.Circle(40);
        Grid.SetColumn(back, 0);
        grid.Add(back);

        var title = SkeletonShapes.Bar(190, 18, width: 148);
        title.VerticalOptions = LayoutOptions.Center;
        title.HorizontalOptions = LayoutOptions.Center;
        Grid.SetColumn(title, 1);
        grid.Add(title);

        var forward = SkeletonShapes.Circle(40);
        Grid.SetColumn(forward, 2);
        grid.Add(forward);

        return grid;
    }

    private static View BuildWeekdayStrip()
    {
        var grid = new Grid { ColumnSpacing = 4 };

        for (var i = 0; i < Columns; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var label = SkeletonShapes.Bar(190, 10, width: 22);
            label.HorizontalOptions = LayoutOptions.Center;
            Grid.SetColumn(label, i);
            grid.Add(label);
        }

        return grid;
    }

    private static View BuildGrid()
    {
        var grid = new Grid
        {
            RowSpacing = 6,
            ColumnSpacing = 4,
            HeightRequest = Weeks * CellHeight
        };

        for (var c = 0; c < Columns; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        for (var r = 0; r < Weeks; r++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        for (var r = 0; r < Weeks; r++)
        {
            for (var c = 0; c < Columns; c++)
            {
                var cell = new Border
                {
                    BackgroundColor = SkeletonPalette.Block,
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) }
                };

                // A few "has events" cells, so the grid does not read as uniform.
                if ((r * Columns + c) % 7 is 2 or 5)
                    cell.BackgroundColor = SkeletonPalette.Ink;

                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Add(cell);
            }
        }

        return grid;
    }

    private static View BuildEventRow()
    {
        var content = SkeletonShapes.HStack(12,
            SkeletonShapes.Bar(190, 12, width: 58),
            SkeletonShapes.Filler(),
            SkeletonShapes.Bar(190, 11, width: 64));

        return SkeletonShapes.Row(64, 16, content);
    }
}