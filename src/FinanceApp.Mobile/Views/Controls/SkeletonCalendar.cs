namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;
using static FinanceApp.Mobile.Views.Controls.SkeletonShapes;

/// <summary>
/// Calendar: header, the month stepper, the seven-column grid inside its card,
/// then the selected day's event rows.
/// <para>
/// The grid was already built at the right shape. What was missing was
/// everything around it: the header, the "Today" pill inside the stepper, and
/// the card the weekday strip and grid actually sit in.
/// </para>
/// </summary>
public class SkeletonCalendar : SkeletonPage
{
    private const int Columns = 7;
    private const int Weeks = 6;
    private const double CellHeight = 42;

    public SkeletonCalendar()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 24) })
    {
    }

    protected override void Populate()
    {
        Host.Children.Add(SkeletonShapes.PageHeader(
            trailing: SkeletonShapes.Circle(46, SkeletonPalette.Surface),
            titleWidth: 84,
            subtitleWidth: 138));

        Host.Children.Add(SkeletonShapes.MonthNavigator());

        Host.Children.Add(BuildGridCard());

        Host.Children.Add(BuildEventsCard());
    }

    /// <summary>
    /// The weekday strip and the month grid, in the bordered card the page puts
    /// them in - 12 of padding, 4 of spacing between the two.
    /// </summary>
    private static View BuildGridCard()
    {
        var weekdays = new Grid { ColumnSpacing = 4 };

        var columns = new ColumnDefinitionCollection();
        for (var i = 0; i < Columns; i++)
            columns.Add(new ColumnDefinition(GridLength.Star));
        weekdays.ColumnDefinitions = columns;

        for (var i = 0; i < Columns; i++)
        {
            var label = SkeletonShapes.Bar(190, 10, width: 24);
            label.HorizontalOptions = LayoutOptions.Center;
            Grid.SetColumn(label, i);
            weekdays.Add(label);
        }

        var grid = new Grid
        {
            RowSpacing = 4,
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
                    BackgroundColor = (r * Columns + c) % 7 is 2 or 5
                        ? SkeletonPalette.Ink
                        : SkeletonPalette.Block,
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) }
                };

                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Add(cell);
            }
        }

        var body = VStack(4, weekdays, grid);

        return new Border
        {
            BackgroundColor = SkeletonPalette.Surface,
            Stroke = new SolidColorBrush(SkeletonPalette.Ink),
            StrokeThickness = 1.5,
            Padding = new Thickness(12),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28) },
            Content = body
        };
    }

    /// <summary>The selected-day card: heading with its Add pill, rows, caption.</summary>
    private static View BuildEventsCard()
    {
        var heading = Bar(190, 16, width: 148);
        heading.VerticalOptions = LayoutOptions.Center;

        var add = SkeletonShapes.ActionPill(56, 34);

        var titleRow = SkeletonShapes.HStack(10, heading, SkeletonShapes.Filler(), add);

        var rows = VStack(8,
            SkeletonShapes.Row(64, 20, SkeletonShapes.HStack(12,
                SkeletonShapes.IconDisc(40),
                SkeletonShapes.Filling(2, Bar(190, 14, width: 76), Bar(190, 12, width: 92)),
                Bar(190, 14, width: 74)), padding: 12),
            SkeletonShapes.Row(64, 20, SkeletonShapes.HStack(12,
                SkeletonShapes.IconDisc(40),
                SkeletonShapes.Filling(2, Bar(190, 14, width: 62), Bar(190, 12, width: 74)),
                Bar(190, 14, width: 74)), padding: 12));

        var caption = Bar(190, 11, width: 176);
        caption.HorizontalOptions = LayoutOptions.Start;

        var body = VStack(12, titleRow, rows, caption);

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