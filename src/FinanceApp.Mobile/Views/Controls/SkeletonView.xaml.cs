namespace FinanceApp.Mobile.Views.Controls;

using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls.Shapes;

/// <summary>
/// Placeholder rows for pages whose content is a list: transactions, accounts,
/// budgets, goals, categories, notifications.
/// <para>
/// The row is shaped to match what it stands in for - leading avatar, a ragged
/// pair of text lines, an optional trailing value and an optional progress line.
/// Matching the shape is the entire point: a generic stack of identical bars
/// still jumps when the data lands, because the real rows are a different height.
/// </para>
/// <para>
/// Only for the first load. Refreshing with data already on screen keeps the data
/// visible, so view models drive this through <c>IsInitialLoading</c> rather than
/// <c>IsBusy</c>.
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

    /// <summary>Leading avatar circle. Off for card-shaped placeholders.</summary>
    public static readonly BindableProperty HasLeadingCircleProperty =
        BindableProperty.Create(nameof(HasLeadingCircle), typeof(bool), typeof(SkeletonView), true, propertyChanged: OnLayoutChanged);

    /// <summary>Trailing bar on the right: an amount, a switch, a badge.</summary>
    public static readonly BindableProperty HasTrailingValueProperty =
        BindableProperty.Create(nameof(HasTrailingValue), typeof(bool), typeof(SkeletonView), false, propertyChanged: OnLayoutChanged);

    /// <summary>Thin full-width line under the text, for budgets and goals.</summary>
    public static readonly BindableProperty HasProgressLineProperty =
        BindableProperty.Create(nameof(HasProgressLine), typeof(bool), typeof(SkeletonView), false, propertyChanged: OnLayoutChanged);

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

    public bool HasTrailingValue
    {
        get => (bool)GetValue(HasTrailingValueProperty);
        set => SetValue(HasTrailingValueProperty, value);
    }

    public bool HasProgressLine
    {
        get => (bool)GetValue(HasProgressLineProperty);
        set => SetValue(HasProgressLineProperty, value);
    }

    public bool Animate
    {
        get => (bool)GetValue(AnimateProperty);
        set => SetValue(AnimateProperty, value);
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
        if (bindable is SkeletonView view)
            view.UpdatePulse();
    }

    /// <summary>
    /// The pulse is stopped when the rows are hidden, not when they are built.
    /// RowCount and friends can be rebound later, and a placeholder nobody is
    /// looking at has no business keeping a 60fps ticker.
    /// </summary>
    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == IsVisibleProperty.PropertyName)
            UpdatePulse();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
            SkeletonPulse.Stop(this);
    }

    /// <summary>
    /// One decision, three callers: a pulse runs only while the rows are both
    /// visible and allowed to animate.
    /// </summary>
    private void UpdatePulse()
    {
        if (Animate && IsVisible)
            SkeletonPulse.Start(this);
        else
            SkeletonPulse.Stop(this);
    }

    private void Rebuild()
    {
        if (RowsHost is null)
            return;

        // Rebuilding mid-animation would leave the old animation attached to
        // elements that are about to be detached.
        SkeletonPulse.Stop(this);
        Opacity = 1;

        RowsHost.Spacing = RowSpacing;
        RowsHost.Clear();

        for (var i = 0; i < Math.Max(0, RowCount); i++)
        {
            RowsHost.Add(BuildRow(i));
        }

        UpdatePulse();
    }

    private View BuildRow(int index)
    {
        var card = new Border
        {
            BackgroundColor = SkeletonPalette.Surface,
            Stroke = new SolidColorBrush(SkeletonPalette.Block),
            StrokeThickness = 1,
            Padding = new Thickness(14),
            HeightRequest = RowHeight,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(CornerRadius) }
        };

        var layout = new HorizontalStackLayout { Spacing = 12, VerticalOptions = LayoutOptions.Center };

        if (HasLeadingCircle)
            layout.Add(SkeletonShapes.Circle(40));

        var lines = SkeletonShapes.Filling(
            // Varying widths is what makes this read as content rather than as
            // a progress bar: real rows have ragged text.
            SkeletonShapes.Bar(190, 12, width: index % 3 == 2 ? 105 : 165),
            SkeletonShapes.Bar(190, 10, width: index % 2 == 0 ? 76 : 118));

        if (HasProgressLine)
            lines.Add(SkeletonShapes.Bar(190, 6, width: index % 3 == 1 ? 120 : 165));

        layout.Add(lines);

        if (HasTrailingValue)
        {
            layout.Add(SkeletonShapes.Trailing(
                SkeletonShapes.Bar(190, 11, width: 64),
                SkeletonShapes.Bar(190, 9, width: 44)));
        }

        card.Content = layout;
        return card;
    }
}