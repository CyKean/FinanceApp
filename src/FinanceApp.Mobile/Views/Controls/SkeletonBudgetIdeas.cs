namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;
using static FinanceApp.Mobile.Views.Controls.SkeletonShapes;

/// <summary>
/// Budget Ideas: header, then a stack of standalone suggestion cards.
/// <para>
/// A suggestion card is 241 tall - the Apply/Dismiss button row alone is 56 -
/// and the old placeholder was 148 with no buttons at all. Over four cards that
/// is a 370pt jump.
/// </para>
/// </summary>
public class SkeletonBudgetIdeas : SkeletonPage
{
    public SkeletonBudgetIdeas()
        : base(new VerticalStackLayout { Spacing = 16, Padding = new Thickness(20, 16, 20, 28) })
    {
    }

    protected override void Populate()
    {
        Host.Children.Add(SkeletonShapes.PageHeader(
            trailing: SkeletonShapes.Circle(46, SkeletonPalette.Surface),
            titleWidth: 132,
            subtitleWidth: 178));

        for (var i = 0; i < 3; i++)
        {
            Host.Children.Add(BuildSuggestion(i));
        }
    }

    /// <summary>
    /// Icon row, the suggested amount beside the current one, a line of prose,
    /// then the Apply/Dismiss buttons: 48 + 10 + 39 + 10 + 20 + 10 + 56 + 32.
    /// </summary>
    private static View BuildSuggestion(int index)
    {
        var heading = SkeletonShapes.Filling(3,
            Bar(190, 15, width: index % 2 == 0 ? 104 : 82),
            Bar(190, 12, width: 68));

        var kind = Bar(190, 20, width: 18);
        kind.VerticalOptions = LayoutOptions.Center;

        var iconRow = SkeletonShapes.HStack(12, SkeletonShapes.IconDisc(48), heading, kind);

        var amount = VStack(2,
            Bar(190, 11, width: 66),
            Bar(190, 20, width: 104));

        var current = Bar(190, 11, width: 62);
        current.VerticalOptions = LayoutOptions.End;
        current.HorizontalOptions = LayoutOptions.End;

        var amountRow = SkeletonShapes.HStack(8, amount, SkeletonShapes.Filler(), current);

        var reason = Bar(190, 13, width: index % 3 == 0 ? 190 : 158);

        var apply = SkeletonShapes.ActionButton(56);
        var dismiss = SkeletonShapes.ActionButton(52, 26, SkeletonPalette.Surface);

        var actions = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(new GridLength(110)) }
        };
        actions.Add(apply);
        Grid.SetColumn(dismiss, 1);
        actions.Add(dismiss);

        var body = VStack(10, iconRow, amountRow, reason, actions);

        return new Border
        {
            BackgroundColor = SkeletonPalette.Surface,
            Stroke = new SolidColorBrush(SkeletonPalette.Ink),
            StrokeThickness = 1.5,
            Padding = new Thickness(16),
            Margin = new Thickness(0, 4, 0, 0),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28) },
            Content = body
        };
    }
}