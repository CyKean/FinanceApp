namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Domain.ValueObjects;

public class AveragesChart : GraphicsView
{
    public static readonly BindableProperty DailyProperty =
        BindableProperty.Create(nameof(Daily), typeof(Money), typeof(AveragesChart), null,
            propertyChanged: OnChartChanged);

    public static readonly BindableProperty MonthlyProperty =
        BindableProperty.Create(nameof(Monthly), typeof(Money), typeof(AveragesChart), null,
            propertyChanged: OnChartChanged);

    public Money? Daily
    {
        get => (Money?)GetValue(DailyProperty);
        set => SetValue(DailyProperty, value);
    }

    public Money? Monthly
    {
        get => (Money?)GetValue(MonthlyProperty);
        set => SetValue(MonthlyProperty, value);
    }

    private readonly AveragesDrawable _drawable = new();

    public AveragesChart()
    {
        Drawable = _drawable;
    }

    private static void OnChartChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var chart = (AveragesChart)bindable;
        chart._drawable.Daily = chart.Daily;
        chart._drawable.Monthly = chart.Monthly;
        chart.Invalidate();
    }
}

internal sealed class AveragesDrawable : IDrawable
{
    private static Color BarColor => FinoraOverlay.Resolve("Error", "#DC2626");

    public Money? Daily { get; set; }
    public Money? Monthly { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        decimal daily = Daily?.Amount ?? 0;
        decimal monthly = Monthly?.Amount ?? 0;

        double max = Math.Max((double)daily, (double)monthly);
        if (max <= 0)
            max = 1;

        var rows = new[]
        {
            new BarRow("Avg Daily", (float)((double)daily / max), daily.ToString("N2"), BarColor),
            new BarRow("Avg Monthly", (float)((double)monthly / max), monthly.ToString("N2"), BarColor)
        };

        BarRowsPainter.Draw(canvas, dirtyRect, rows);
    }
}
