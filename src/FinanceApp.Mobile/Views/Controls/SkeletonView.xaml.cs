namespace FinanceApp.Mobile.Views.Controls;

/// <summary>
/// Placeholder rows shown while a page's first load is in flight.
/// <para>
/// Skeletons rather than a spinner because the point is to show the shape of what
/// is arriving: a spinner says "wait", a skeleton says "this is what you are
/// about to get", and the layout does not jump when the data lands.
/// </para>
/// <para>
/// Only for the first load. Refreshing with data already on screen should keep
/// the data visible, so the view models set the flag rather than binding to
/// <c>IsBusy</c> - otherwise every pull-to-refresh would blank the page.
/// </para>
/// </summary>
public partial class SkeletonView : ContentView
{
    public static readonly BindableProperty RowCountProperty =
        BindableProperty.Create(nameof(RowCount), typeof(int), typeof(SkeletonView), 5, propertyChanged: OnLayoutChanged);

    public static readonly BindableProperty RowHeightProperty =
        BindableProperty.Create(nameof(RowHeight), typeof(double), typeof(SkeletonView), 76d, propertyChanged: OnLayoutChanged);

    public static readonly BindableProperty RowSpacingProperty =
        BindableProperty.Create(nameof(RowSpacing), typeof(double), typeof(SkeletonView), 10d, propertyChanged: OnLayoutChanged);

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(SkeletonView), 18d, propertyChanged: OnLayoutChanged);

    /// <summary>Draws the leading circle. Turn off for card-shaped placeholders.</summary>
    public static readonly BindableProperty HasLeadingCircleProperty =
        BindableProperty.Create(nameof(HasLeadingCircle), typeof(bool), typeof(SkeletonView), true, propertyChanged: OnLayoutChanged);

    public static readonly BindableProperty AnimateProperty =
        BindableProperty.Create(nameof(Animate), typeof(bool), typeof(SkeletonView), true, propertyChanged: OnAnimateChanged);

    public int RowCount
    {
        get => (int)GetValue(RowCountProperty);
        set => SetValue(RowCountProperty, value);
    }

    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public bool HasLeadingCircle
    {
        get => (bool)GetValue(HasLeadingCircleProperty);
        set => SetValue(HasLeadingCircleProperty, value);
    }

    public bool Animate
    {
        get => (bool)GetValue(AnimateProperty);
        set => SetValue(AnimateProperty, value);
    }

    /// <summary>Palette, resolved from the app resources rather than hard-coded.</summary>
    private static Color BlockColor => Resolve("FinoraCreamDeep", Color.FromArgb("#E4EACB"));
    private static Color SurfaceColor => Resolve("FinoraCard", Color.FromArgb("#FAFBF0"));

    private static Color Resolve(string key, Color fallback)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
            return color;

        return fallback;
    }

    public SkeletonView()
    {
        InitializeComponent();
        Rebuild();
    }

    private static void OnLayoutChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkeletonView view)
            view.Rebuild();
    }

    private static void OnAnimateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (newValue is true && oldValue is false && bindable is SkeletonView view)
            view.StartPulse();
    }

    private void Rebuild()
    {
        if (RowsHost is null)
            return;

        // Rebuilding mid-animation would leave the old animation attached to
        // detached elements, so stop before swapping the tree.
        this.AbortAnimation(PulseName);
        Opacity = 1;

        RowsHost.Spacing = RowSpacing;
        RowsHost.Clear();

        for (var i = 0; i < Math.Max(0, RowCount); i++)
        {
            RowsHost.Add(BuildRow(i));
        }

        if (Animate)
            StartPulse();
    }

    private View BuildRow(int index)
    {
        var card = new Border
        {
            BackgroundColor = SurfaceColor,
            Stroke = new SolidColorBrush(BlockColor),
            StrokeThickness = 1,
            Padding = new Thickness(14),
            HeightRequest = RowHeight,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(CornerRadius) }
        };

        var content = new HorizontalStackLayout { Spacing = 12, VerticalOptions = LayoutOptions.Center };

        if (HasLeadingCircle)
        {
            content.Add(new Border
            {
                WidthRequest = 40,
                HeightRequest = 40,
                BackgroundColor = BlockColor,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse()
            });
        }

        // Varying the line widths is what makes a skeleton read as content
        // rather than as a loading bar: real rows have ragged text.
        var lines = new VerticalStackLayout { Spacing = 7, VerticalOptions = LayoutOptions.Center };
        lines.Add(Bar(widthFraction: index % 3 == 2 ? 0.55 : 0.85, height: 12));
        lines.Add(Bar(widthFraction: index % 2 == 0 ? 0.40 : 0.62, height: 10));

        content.Add(lines);
        card.Content = content;

        return card;
    }

    private static Border Bar(double widthFraction, double height) => new()
    {
        HeightRequest = height,
        WidthRequest = Math.Max(60, 190 * widthFraction),
        HorizontalOptions = LayoutOptions.Start,
        BackgroundColor = BlockColor,
        StrokeThickness = 0,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(height / 2) }
    };

    private const string PulseName = "skeleton-pulse";

    private void StartPulse()
    {
        if (!Animate || RowCount == 0)
            return;

        var animation = new Animation(
            value => Opacity = value,
            0.55,
            1.0,
            Easing.CubicInOut);

        animation.Commit(this, PulseName, length: 850, easing: Easing.SinInOut, finished: null, repeat: () => true);
    }
}