namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;

/// <summary>
/// Building blocks for the placeholder shapes.
/// <para>
/// The palette lives here rather than in each template so a skeleton can never
/// drift from the app theme, and so the per-page templates are pure layout.
/// </para>
/// </summary>
public static class SkeletonPalette
{
    public static Color Block => Resolve("FinoraCreamDeep", Color.FromArgb("#E4EACB"));
    public static Color Surface => Resolve("FinoraCard", Color.FromArgb("#FAFBF0"));
    public static Color Ink => Resolve("FinoraInk", Color.FromArgb("#161B16"));

    private static Color Resolve(string key, Color fallback) =>
        Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : fallback;
}

/// <summary>A single placeholder bar, circle or slab.</summary>
public static class SkeletonShapes
{
    /// <summary>A bar whose width is a fraction of <paramref name="of"/>.</summary>
    public static Border Bar(double of, double height, double? width = null, Color? fill = null) => new()
    {
        HeightRequest = height,
        // `of` is only a fallback: callers that know the real width pass it, so
        // the placeholder can be sized against the actual layout.
        WidthRequest = width ?? of,
        HorizontalOptions = LayoutOptions.Start,
        BackgroundColor = fill ?? SkeletonPalette.Block,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(height / 2) }
    };

    public static Border Circle(double size, Color? fill = null) => new()
    {
        WidthRequest = size,
        HeightRequest = size,
        BackgroundColor = fill ?? SkeletonPalette.Block,
        StrokeThickness = 0,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse()
    };

    /// <summary>A rounded slab, for standing in for a card or chart.</summary>
    public static Border Slab(double height, double? width = null, Color? fill = null, double radius = 20)
    {
        var slab = new Border
        {
            HeightRequest = height,
            BackgroundColor = fill ?? SkeletonPalette.Surface,
            Stroke = new SolidColorBrush(SkeletonPalette.Block),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) }
        };

        // WidthRequest is left unset when the slab should fill its slot, rather
        // than assigned null, which is not a valid double for the property.
        if (width is not null)
            slab.WidthRequest = width.Value;

        return slab;
    }

    /// <summary>Card container matching the page's own bordered-card style.</summary>
    public static Border Card(double height, double radius = 18, Color? fill = null)
    {
        var card = Slab(height, fill: fill, radius: radius);
        card.Padding = new Thickness(14);
        return card;
    }

    // Small builders, because these templates are otherwise wall-to-wall
    // object initialisers.

    public static VerticalStackLayout VStack(double spacing, params View[] children)
    {
        var stack = new VerticalStackLayout { Spacing = spacing };
        foreach (var child in children)
            stack.Add(child);

        return stack;
    }

    public static HorizontalStackLayout HStack(double spacing, params View[] children)
    {
        var row = new HorizontalStackLayout { Spacing = spacing };
        foreach (var child in children)
            row.Add(child);

        return row;
    }

    /// <summary>Zero-width filler that pushes what follows to the trailing edge.</summary>
    public static BoxView Filler() => new() { WidthRequest = 0, HorizontalOptions = LayoutOptions.Fill };

    /// <summary>Centre this stack within its row.</summary>
    public static VerticalStackLayout Centered(params View[] children)
    {
        var stack = VStack(7, children);
        stack.VerticalOptions = LayoutOptions.Center;
        return stack;
    }

    /// <summary>This stack fills horizontally and pushes trailing content right.</summary>
    public static VerticalStackLayout Filling(params View[] children)
    {
        var stack = VStack(7, children);
        stack.VerticalOptions = LayoutOptions.Center;
        stack.HorizontalOptions = LayoutOptions.Fill;
        return stack;
    }

    /// <summary>This stack hugs the trailing edge of its row.</summary>
    public static VerticalStackLayout Trailing(params View[] children)
    {
        var stack = VStack(7, children);
        stack.VerticalOptions = LayoutOptions.Center;
        stack.HorizontalOptions = LayoutOptions.End;
        return stack;
    }

    /// <summary>
    /// A bordered row matching the app's list-row cards. Shared by the
    /// page-specific skeletons so they do not each reinvent the shape.
    /// </summary>
    public static Border Row(double height, double radius, View content)
    {
        var card = new Border
        {
            BackgroundColor = SkeletonPalette.Surface,
            Stroke = new SolidColorBrush(SkeletonPalette.Block),
            StrokeThickness = 1,
            Padding = new Thickness(14),
            HeightRequest = height,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) },
            Content = content
        };

        return card;
    }

    /// <summary>A dark slab for the hero/balance cards that invert the palette.</summary>
    public static Border DarkCard(double height, double radius, params View[] children)
    {
        var card = new Border
        {
            BackgroundColor = FinanceApp.Mobile.Helpers.FinoraOverlay.Resolve("FinoraInkSoft", "#232923"),
            StrokeThickness = 0,
            Padding = new Thickness(18),
            HeightRequest = height,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) },
            Content = Centered(children)
        };

        return card;
    }

    /// <summary>A thin progress line, for budgets and goals.</summary>
    public static Border ProgressLine(double width, double height = 7) =>
        Bar(width, height, width: width);

    /// <summary>An <paramref name="of"/>-wide track with a shorter filled portion.</summary>
    public static View ProgressBar(double of, double fillFraction, double height = 7)
    {
        var track = new Border
        {
            HeightRequest = height,
            BackgroundColor = SkeletonPalette.Block,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(height / 2) },
            HorizontalOptions = LayoutOptions.Start,
            WidthRequest = of
        };

        var fill = Bar(of, height, width: Math.Max(8, of * fillFraction));
        return HStack(0, track, fill);
    }

    /// <summary>A circle the size of the app's circular action buttons.</summary>
    public static Border CircleButton(double size = 44) => Circle(size);
}