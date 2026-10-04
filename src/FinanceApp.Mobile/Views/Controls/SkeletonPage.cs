namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;

/// <summary>
/// Shared plumbing for the page-specific skeletons.
/// <para>
/// Each page gets its own class because a placeholder of the wrong shape still
/// resizes the page when the data lands, which is the problem skeletons exist to
/// solve. That would otherwise mean nine near-identical ContentViews each
/// re-implementing a pulse, so it lives here once and the page classes are just
/// the layout.
/// </para>
/// <para>
/// Code-only by design. These build their whole tree in code, and the MAUI XAML
/// compiler on the Windows target rejected a XAML shell for one of these while
/// accepting identical shells on its siblings.
/// </para>
/// </summary>
public abstract class SkeletonPage : ContentView
{
    private const string PulseName = "skeleton-pulse";

    protected SkeletonPage(VerticalStackLayout host)
    {
        Host = host;
        Content = host;

        Rebuild();
        StartPulse();
    }

    protected VerticalStackLayout Host { get; }

    /// <summary>Rebuilds the placeholder tree. Called whenever a shape property changes.</summary>
    protected abstract void Populate();

    /// <summary>Called after every rebuild so subclasses can re-measure themselves.</summary>
    protected virtual void OnPopulated()
    {
    }

    protected void Invalidate() => Rebuild();

    private void Rebuild()
    {
        // Rebuilding mid-animation would leave the old animation attached to
        // elements that are about to be detached.
        this.AbortAnimation(PulseName);
        Opacity = 1;

        Host.Children.Clear();
        Populate();
        OnPopulated();

        StartPulse();
    }

    protected static View SectionHeading(double width = 124) => SkeletonShapes.Bar(190, 15, width: width);

    /// <summary>A run of equal stat tiles, as the analytics and forecast pages have.</summary>
    protected static View StatTiles(int count, double height = 92)
    {
        var grid = new Grid { ColumnSpacing = 12 };

        var columns = new ColumnDefinitionCollection();
        for (var i = 0; i < Math.Max(1, count); i++)
            columns.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions = columns;

        for (var i = 0; i < Math.Max(0, count); i++)
        {
            var tile = SkeletonShapes.Card(height, radius: 18);
            tile.Content = SkeletonShapes.VStack(8,
                SkeletonShapes.Bar(190, 10, width: 62),
                SkeletonShapes.Bar(190, 20, width: 84));

            Grid.SetColumn(tile, i);
            grid.Add(tile);
        }

        return grid;
    }

    /// <summary>A chart-shaped card: title, optional legend, and an optional body bar.</summary>
    protected static View ChartCard(double height, double titleWidth = 150, bool legend = true, double bodyHeight = 0)
    {
        var children = new List<View> { SkeletonShapes.Bar(190, 14, width: titleWidth) };

        if (legend)
        {
            children.Add(SkeletonShapes.HStack(14,
                SkeletonShapes.Circle(12),
                SkeletonShapes.Bar(190, 10, width: 58),
                SkeletonShapes.Circle(12),
                SkeletonShapes.Bar(190, 10, width: 44)));
        }

        if (bodyHeight > 0)
            children.Add(SkeletonShapes.Bar(190, bodyHeight, width: 190));

        var card = SkeletonShapes.Card(height, radius: 20);
        card.Content = SkeletonShapes.VStack(14, children.ToArray());
        return card;
    }

    private void StartPulse()
    {
        var animation = new Animation(value => Opacity = value, 0.55, 1.0, Easing.CubicInOut);
        animation.Commit(this, PulseName, length: 850, easing: Easing.SinInOut, finished: null, repeat: () => true);
    }
}