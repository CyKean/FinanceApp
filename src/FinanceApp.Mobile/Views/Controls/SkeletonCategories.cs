namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;

/// <summary>
/// Categories: a short row per category with a leading initial tile and a trailing
/// toggle, which is what distinguishes this list from the others.
/// </summary>
public class SkeletonCategories : SkeletonPage
{
    public SkeletonCategories()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 24) })
    {
    }

    protected override void Populate()
    {
        for (var i = 0; i < 7; i++)
        {
            Host.Children.Add(BuildRow(i));
        }
    }

    private static View BuildRow(int index)
    {
        var content = SkeletonShapes.HStack(12,
            SkeletonShapes.Slab(38, width: 38, radius: 12),
            SkeletonShapes.Filling(
                SkeletonShapes.Bar(190, 12, width: index % 3 == 1 ? 108 : 142),
                SkeletonShapes.Bar(190, 10, width: index % 2 == 0 ? 66 : 88)),
            BuildSwitch(index % 4 != 0));

        return SkeletonShapes.Row(72, 16, content);
    }

    /// <summary>A pill track with its knob pushed to one side, as the page's Switch renders.</summary>
    private static View BuildSwitch(bool isOn)
    {
        var track = new Border
        {
            WidthRequest = 46,
            HeightRequest = 26,
            BackgroundColor = SkeletonPalette.Block,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(13) },
            VerticalOptions = LayoutOptions.Center
        };

        var knob = SkeletonShapes.Circle(20);
        knob.VerticalOptions = LayoutOptions.Center;
        knob.HorizontalOptions = isOn ? LayoutOptions.End : LayoutOptions.Start;
        knob.Margin = new Thickness(isOn ? 0 : 3, 0, isOn ? 3 : 0, 0);

        return SkeletonShapes.HStack(0, track, knob);
    }
}