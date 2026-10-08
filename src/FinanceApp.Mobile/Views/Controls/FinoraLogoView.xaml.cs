namespace FinanceApp.Mobile.Views.Controls;

/// <summary>
/// The Finora app mark: a lime tile with the ink trajectory knockout, matching
/// Resources/AppIcon/finora_fg.svg and the splash screen. Drawn in XAML so it
/// follows the active theme and can be sized anywhere.
/// </summary>
public partial class FinoraLogoView : ContentView
{
    /// <summary>Edge length of the tile in device-independent units.</summary>
    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(nameof(Size), typeof(double), typeof(FinoraLogoView), 40.0,
            propertyChanged: (b, _, value) => ((FinoraLogoView)b).ApplySize((double)value));

    public FinoraLogoView()
    {
        InitializeComponent();
        ApplySize(Size);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    private void ApplySize(double size)
    {
        var s = size <= 0 ? 40 : size;
        Root.WidthRequest = s;
        Root.HeightRequest = s;

        // The tile is authored at 301 units; scale that canvas down to the
        // requested edge so one geometry serves every size in the app.
        var scale = s / 301.0;
        Canvas.ScaleX = scale;
        Canvas.ScaleY = scale;
    }
}
