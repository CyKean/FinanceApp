namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Helpers;
using Microsoft.Maui.Controls.Shapes;

public partial class FinoraIconView : ContentView
{
    private static readonly Color Ink = Color.FromArgb("#161B16");
    private static readonly Color Lime = Color.FromArgb("#CDF463");
    private readonly Ellipse _circle = new();

    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(string), typeof(FinoraIconView), null,
            propertyChanged: (b, _, _) => ((FinoraIconView)b).Refresh());

    public static readonly BindableProperty NameProperty =
        BindableProperty.Create(nameof(Name), typeof(string), typeof(FinoraIconView), null,
            propertyChanged: (b, _, _) => ((FinoraIconView)b).Refresh());

    public static readonly BindableProperty IconKeyProperty =
        BindableProperty.Create(nameof(IconKey), typeof(string), typeof(FinoraIconView), null,
            propertyChanged: (b, _, _) => ((FinoraIconView)b).Refresh());

    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(nameof(Size), typeof(double), typeof(FinoraIconView), 46.0,
            propertyChanged: (b, _, _) => ((FinoraIconView)b).Refresh());

    public static readonly BindableProperty LightProperty =
        BindableProperty.Create(nameof(Light), typeof(bool), typeof(FinoraIconView), false,
            propertyChanged: (b, _, _) => ((FinoraIconView)b).Refresh());

    public static readonly BindableProperty ShowBadgeProperty =
        BindableProperty.Create(nameof(ShowBadge), typeof(bool), typeof(FinoraIconView), true,
            propertyChanged: (b, _, _) => ((FinoraIconView)b).Refresh());

    public static readonly BindableProperty AccentProperty =
        BindableProperty.Create(nameof(Accent), typeof(Color), typeof(FinoraIconView), null,
            propertyChanged: (b, _, _) => ((FinoraIconView)b).Refresh());

    /// <summary>Raw stored icon (usually an emoji) used for key resolution.</summary>
    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Owner name (category / account / goal) used for keyword resolution.</summary>
    public string? Name
    {
        get => (string?)GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <summary>Direct icon key (e.g. "calendar") — skips emoji/name resolution.</summary>
    public string? IconKey
    {
        get => (string?)GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    /// <summary>Badge diameter (or glyph size when the badge is hidden).</summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Lime badge with ink glyph instead of ink badge with white glyph.</summary>
    public bool Light
    {
        get => (bool)GetValue(LightProperty);
        set => SetValue(LightProperty, value);
    }

    /// <summary>False renders the bare glyph with no circle (for already-circled hosts).</summary>
    public bool ShowBadge
    {
        get => (bool)GetValue(ShowBadgeProperty);
        set => SetValue(ShowBadgeProperty, value);
    }

    /// <summary>
    /// Optional badge fill that overrides the ink/lime pairing. Used where a
    /// severity has to read at a glance (critical red vs info blue). The glyph
    /// colour is picked automatically for contrast against the accent.
    /// </summary>
    public Color? Accent
    {
        get => (Color?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public FinoraIconView()
    {
        InitializeComponent();
        Refresh();
    }

    /// <summary>
    /// The icon key this instance last parsed.
    /// <para>
    /// Every one of the seven bindable properties calls Refresh, and setting them
    /// from XAML means a full SVG-path tokenize per property per list row - on the
    /// UI thread, while a list is being realised. Parsing is only skipped when the
    /// resolved key is unchanged, so the geometry instance still belongs to this
    /// control alone: sharing one across Path elements is what breaks Android.
    /// </para>
    /// </summary>
    private string? _parsedKey;

    private void Refresh()
    {
        if (Badge == null || Glyph == null)
            return;

        var key = FinoraIcons.Resolve(Icon, Name, IconKey);
        if (!string.Equals(key, _parsedKey, StringComparison.Ordinal))
        {
            _parsedKey = key;
            Glyph.Data = FinoraIcons.GetGeometry(key);
        }

        var accent = Accent;
        Glyph.Stroke = accent is not null ? ContrastOn(accent) : Light ? Ink : Colors.White;

        var size = Size <= 0 ? 46 : Size;
        // Explicit glyph size, exactly like the (working) tab bar pattern.
        var glyphSize = ShowBadge ? size * 0.46 : size;
        Glyph.WidthRequest = glyphSize;
        Glyph.HeightRequest = glyphSize;

        if (ShowBadge)
        {
            Badge.WidthRequest = size;
            Badge.HeightRequest = size;
            Badge.Background = accent ?? (Light ? Lime : Ink);
            Badge.StrokeShape = _circle;
            Badge.Padding = 0;
        }
        else
        {
            // Invisible wrapper: bare glyph only.
            Badge.WidthRequest = -1;
            Badge.HeightRequest = -1;
            Badge.Background = Colors.Transparent;
            Badge.StrokeShape = null;
            Badge.Padding = 0;
        }
    }

    /// <summary>Picks ink or white glyph depending on how bright the badge is.</summary>
    private static Color ContrastOn(Color color)
    {
        var luminance = (0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue);
        return luminance > 0.6 ? Ink : Colors.White;
    }
}
