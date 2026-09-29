namespace FinanceApp.Mobile.Views.Controls;

public class SavingsRateGauge : GraphicsView
{
    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(double?), typeof(SavingsRateGauge), null,
            propertyChanged: OnChartChanged);

    public double? Value
    {
        get => (double?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private readonly SavingsRateGaugeDrawable _drawable = new();

    public SavingsRateGauge()
    {
        Drawable = _drawable;
    }

    private static void OnChartChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var gauge = (SavingsRateGauge)bindable;
        gauge._drawable.Value = gauge.Value;
        gauge.Invalidate();
    }
}

internal sealed class SavingsRateGaugeDrawable : IDrawable
{
    public double? Value { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float size = MathF.Min(dirtyRect.Width, dirtyRect.Height) - 4f;
        if (size <= 0)
            return;

        float cx = dirtyRect.Left + dirtyRect.Width / 2;
        float cy = dirtyRect.Top + dirtyRect.Height / 2;
        float outer = size / 2;
        float inner = outer * 0.70f;

        canvas.FillColor = ThemeResources.GetColor("OutlineVariant", "OutlineVariantDark");
        canvas.FillPath(RingPath.Build(cx, cy, outer, inner, -90f, 270f));

        if (Value is not double rate)
            return;

        var sweep = (float)(Math.Clamp(rate, 0, 100) / 100 * 360);
        if (sweep > 0.5f)
        {
            canvas.FillColor = rate >= 20
                ? Color.FromArgb("#16A34A")
                : rate >= 10
                    ? Color.FromArgb("#F97316")
                    : rate >= 0
                        ? Color.FromArgb("#EAB308")
                        : Color.FromArgb("#DC2626");
            canvas.FillPath(RingPath.Build(cx, cy, outer, inner, -90f, -90f + sweep));
        }

        canvas.FontColor = ThemeResources.GetColor("OnSurface", "OnSurfaceDark");
        canvas.FontSize = 26;
        canvas.DrawString(rate.ToString("F1") + "%",
            cx - inner + 6f, cy - 18f, (inner - 6f) * 2, 36f,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
