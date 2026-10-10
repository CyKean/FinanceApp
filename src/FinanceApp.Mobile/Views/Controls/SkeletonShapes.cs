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
    public static Color Lime => Resolve("FinoraLime", Color.FromArgb("#CDF463"));
    public static Color InkSoft => Resolve("FinoraInkSoft", Color.FromArgb("#232923"));

    /// <summary>
    /// Placeholder fill for content sitting on a dark or lime card. The dark
    /// cards use <see cref="Ink"/> as their fill, so a placeholder painted the
    /// default block colour on top of one is invisible - which is why the hero
    /// skeletons used to look like empty boxes.
    /// </summary>
    public static Color OnDark => Resolve("FinoraCreamDeep", Color.FromArgb("#E4EACB"));

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
    public static VerticalStackLayout Centered(params View[] children) => Centered(7, children);

    /// <summary>As <see cref="Centered(View[])"/>, at an explicit line spacing.</summary>
    public static VerticalStackLayout Centered(double spacing, params View[] children)
    {
        var stack = VStack(spacing, children);
        stack.VerticalOptions = LayoutOptions.Center;
        return stack;
    }

    /// <summary>This stack fills horizontally and pushes trailing content right.</summary>
    public static VerticalStackLayout Filling(params View[] children) => Filling(7, children);

    /// <summary>As <see cref="Filling(View[])"/>, at an explicit line spacing.</summary>
    public static VerticalStackLayout Filling(double spacing, params View[] children)
    {
        var stack = VStack(spacing, children);
        stack.VerticalOptions = LayoutOptions.Center;
        stack.HorizontalOptions = LayoutOptions.Fill;
        return stack;
    }

    /// <summary>This stack hugs the trailing edge of its row.</summary>
    public static VerticalStackLayout Trailing(params View[] children) => Trailing(7, children);

    /// <summary>As <see cref="Trailing(View[])"/>, at an explicit line spacing.</summary>
    public static VerticalStackLayout Trailing(double spacing, params View[] children)
    {
        var stack = VStack(spacing, children);
        stack.VerticalOptions = LayoutOptions.Center;
        stack.HorizontalOptions = LayoutOptions.End;
        return stack;
    }

/// <summary>
/// A bordered row matching the app's list-row cards. Shared by the
/// page-specific skeletons so they do not each reinvent the shape.
/// </summary>
    public static Border Row(double height, double radius, View content, double padding = 14)
    {
        var card = new Border
        {
            BackgroundColor = SkeletonPalette.Surface,
            Stroke = new SolidColorBrush(SkeletonPalette.Block),
            StrokeThickness = 1,
            Padding = new Thickness(padding),
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

    /// <summary>
    /// A card that inverts the palette - the lime and dark stat tiles the pages
    /// use for headline figures. <paramref name="barFill"/> must be the on-card
    /// colour, because the default block colour vanishes against both.
    /// </summary>
    public static Border ContrastCard(double height, double radius, Color background, Color barFill, params View[] children)
    {
        var card = new Border
        {
            BackgroundColor = background,
            StrokeThickness = 0,
            Padding = new Thickness(14),
            HeightRequest = height,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) }
        };

        var stack = VStack(2);
        foreach (var child in children)
            stack.Add(child);

        stack.VerticalOptions = LayoutOptions.Center;
        card.Content = stack;
        return card;
    }

    /// <summary>
    /// The circular category/account badge the rows use: a filled ink disc with
    /// a light glyph in it.
    /// </summary>
    public static View IconDisc(double size)
    {
        var grid = new Grid { WidthRequest = size, HeightRequest = size };

        var disc = Circle(size, SkeletonPalette.Ink);
        Grid.SetRow(disc, 0);
        Grid.SetColumn(disc, 0);
        grid.Add(disc);

        // Stands in for the glyph plus its little chevron.
        var glyph = Circle(size * 0.3, SkeletonPalette.OnDark);
        glyph.HorizontalOptions = LayoutOptions.Center;
        glyph.VerticalOptions = LayoutOptions.Center;
        Grid.SetRow(glyph, 0);
        Grid.SetColumn(glyph, 0);
        grid.Add(glyph);

        return grid;
    }

    /// <summary>
    /// The header row every pushed page opens with: a leading circle, a title
    /// and subtitle, and a trailing action. Nine pages share this shape, and a
    /// placeholder that omits it makes the whole page shift down by a header's
    /// worth of height the moment data lands.
    /// </summary>
    public static View PageHeader(View? trailing = null, double titleWidth = 120, double subtitleWidth = 168)
    {
        var text = VStack(0,
            Bar(190, 18, width: titleWidth),
            Bar(190, 12, width: subtitleWidth));
        text.VerticalOptions = LayoutOptions.Center;
        text.HorizontalOptions = LayoutOptions.Fill;

        return HStack(10, Circle(46), text, Filler(), trailing ?? Circle(46));
    }

    /// <summary>
    /// The collapsed filter control: a pill on the leading edge and the active
    /// filter summary trailing.
    /// </summary>
    public static View FilterBarRow(double pillWidth = 74, double summaryWidth = 96)
    {
        var pill = Slab(40, width: pillWidth, fill: SkeletonPalette.Surface, radius: 20);

        var summary = Bar(190, 12, width: summaryWidth);
        summary.HorizontalOptions = LayoutOptions.End;
        summary.VerticalOptions = LayoutOptions.Center;

        return HStack(0, pill, summary);
    }

    /// <summary>A lime action pill, as used for the header's trailing button.</summary>
    public static View ActionPill(double width, double height = 34) =>
        Slab(height, width: width, fill: SkeletonPalette.Lime, radius: height / 2);

    /// <summary>
    /// The dark balance hero the dashboard and accounts both open with: five
    /// lines of copy beside the lime contactless bar, with the action pill
    /// straddling its top edge. 173 of card plus the 15pt gap above it, which is
    /// what the page's own Margin produces.
    /// </summary>
    public static Border BalanceHero()
    {
        var copy = VStack(2,
            Bar(190, 14, width: 44, fill: SkeletonPalette.OnDark),
            Bar(190, 14, width: 78, fill: SkeletonPalette.OnDark),
            Bar(190, 30, width: 134, fill: SkeletonPalette.OnDark),
            Bar(190, 14, width: 120, fill: SkeletonPalette.OnDark),
            Bar(190, 13, width: 104, fill: SkeletonPalette.OnDark));
        copy.VerticalOptions = LayoutOptions.Start;

        var contactless = new Border
        {
            WidthRequest = 38,
            BackgroundColor = SkeletonPalette.Lime,
            StrokeThickness = 0,
            VerticalOptions = LayoutOptions.Fill,
            Margin = new Thickness(10, 20, 4, 20),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(100) }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Star),
                new(new GridLength(54))
            }
        };

        grid.Add(copy);
        Grid.SetColumn(contactless, 1);
        grid.Add(contactless);

        return new Border
        {
            BackgroundColor = SkeletonPalette.InkSoft,
            Stroke = new SolidColorBrush(SkeletonPalette.Ink),
            StrokeThickness = 1.5,
            Padding = new Thickness(20, 20, 8, 20),
            HeightRequest = 173,
            Margin = new Thickness(0, 15, 0, 0),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(34) },
            Content = grid
        };
    }

    /// <summary>
    /// The pill switcher that sits under a hero card - Accounts/History on the
    /// accounts page, Expenses/Income on the categories page.
    /// </summary>
    public static View SegmentedTabs(int count, double segmentHeight = 41, int activeIndex = 0, double labelWidth = 62)
    {
        var grid = new Grid { ColumnSpacing = 6 };

        var columns = new ColumnDefinitionCollection();
        for (var i = 0; i < Math.Max(1, count); i++)
            columns.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions = columns;

        for (var i = 0; i < count; i++)
        {
            var active = i == activeIndex;

            var label = Bar(190, 13, width: active ? labelWidth : labelWidth - 12, fill: SkeletonPalette.Surface);
            label.HorizontalOptions = LayoutOptions.Center;
            label.VerticalOptions = LayoutOptions.Center;

            var segment = new Border
            {
                Padding = new Thickness(0, 12),
                BackgroundColor = active ? SkeletonPalette.Lime : Colors.Transparent,
                Stroke = active ? new SolidColorBrush(SkeletonPalette.Ink) : null,
                StrokeThickness = active ? 1.5 : 0,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(100) },
                Content = label
            };

            Grid.SetColumn(segment, i);
            grid.Add(segment);
        }

        return new Border
        {
            BackgroundColor = SkeletonPalette.Surface,
            Stroke = new SolidColorBrush(SkeletonPalette.Ink),
            StrokeThickness = 1.5,
            Padding = new Thickness(6),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(100) },
            Content = grid
        };
    }

    /// <summary>
    /// The month stepper shared by budgets and calendar: two circular arrows
    /// around a centred month label. 42 of control inside 8 of padding.
    /// </summary>
    public static View MonthNavigator(double labelWidth = 96)
    {
        var label = Bar(190, 15, width: labelWidth);
        label.HorizontalOptions = LayoutOptions.Center;
        label.VerticalOptions = LayoutOptions.Center;

        var grid = new Grid
        {
            ColumnSpacing = 8,
            VerticalOptions = LayoutOptions.Center,
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Star),
                new(GridLength.Auto),
                new(GridLength.Auto)
            }
        };

        var previous = Circle(42, SkeletonPalette.Surface);
        grid.Add(previous);

        Grid.SetColumn(label, 1);
        grid.Add(label);

        var today = Slab(34, width: 48, fill: SkeletonPalette.Surface, radius: 100);
        Grid.SetColumn(today, 2);
        grid.Add(today);

        var next = Circle(42, SkeletonPalette.Ink);
        Grid.SetColumn(next, 3);
        grid.Add(next);

        return new Border
        {
            BackgroundColor = SkeletonPalette.Surface,
            Stroke = new SolidColorBrush(SkeletonPalette.Ink),
            StrokeThickness = 1.5,
            Padding = new Thickness(8),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(100) },
            Content = grid
        };
    }

    /// <summary>
    /// A full-width primary action button. The page's own height, because a
    /// button that is not there at all shifts everything above it.
    /// </summary>
    public static View ActionButton(double height = 56, double radius = 26, Color? fill = null) =>
        Slab(height, fill: fill ?? SkeletonPalette.Ink, radius: radius);

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