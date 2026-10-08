namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Application.DTOs;

/// <summary>
/// Overview bar chart driven entirely by <see cref="StatisticsChartPointDto"/>
/// values from the statistics pipeline. Bar heights are proportional to the
/// values and the Y-axis scales to the data, never to a fixed mock range.
/// </summary>
public class OverviewChart : GraphicsView
{
    public static readonly BindableProperty ItemsProperty =
        BindableProperty.Create(nameof(Items), typeof(IReadOnlyList<StatisticsChartPointDto>), typeof(OverviewChart), null,
            propertyChanged: (b, _, v) => ((OverviewChart)b).OnItemsChanged(v));

    public static readonly BindableProperty TooltipTextProperty =
        BindableProperty.Create(nameof(TooltipText), typeof(string), typeof(OverviewChart), string.Empty,
            propertyChanged: (b, _, v) => ((OverviewChart)b).Invalidate());

    public IReadOnlyList<StatisticsChartPointDto>? Items
    {
        get => (IReadOnlyList<StatisticsChartPointDto>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Description of the selected bucket; tapping a bar group updates it.</summary>
    public string TooltipText
    {
        get => (string)GetValue(TooltipTextProperty);
        set => SetValue(TooltipTextProperty, value);
    }

    private readonly OverviewChartDrawable _drawable = new();

    public OverviewChart()
    {
        Drawable = _drawable;
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTap;
        GestureRecognizers.Add(tap);
    }

    private void OnTap(object? sender, TappedEventArgs e)
    {
        var p = e.GetPosition(this);
        if (p is null || _drawable.Items is null || _drawable.Items.Count == 0)
            return;

        var count = _drawable.Items.Count;
        var index = (int)(p.Value.X / Math.Max(1f, (float)Width / count));
        index = Math.Clamp(index, 0, count - 1);
        var point = _drawable.Items[index];
        TooltipText = $"{point.Label}: Spending {point.Spending} · Earning {point.Earning}";
        _drawable.SelectedIndex = index;
        Invalidate();
    }

    private void OnItemsChanged(object? newValue)
    {
        _drawable.Items = newValue as IReadOnlyList<StatisticsChartPointDto>;
        _drawable.SelectedIndex = -1;
        Invalidate();
    }
}

internal sealed class OverviewChartDrawable : IDrawable
{
    private static Color SpendingColor => FinoraOverlay.Resolve("FinoraInk", "#161B16");
    private static Color EarningColor => FinoraOverlay.Resolve("FinoraLime", "#CDF463");

    public IReadOnlyList<StatisticsChartPointDto>? Items { get; set; }
    public int SelectedIndex { get; set; } = -1;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var items = Items;
        if (items == null || items.Count == 0 || dirtyRect.Height <= 0)
            return;

        const float axisWidth = 44f;
        const float labelHeight = 22f;
        float plotLeft = dirtyRect.Left + axisWidth;
        float plotRight = dirtyRect.Right - 4f;
        float plotTop = dirtyRect.Top + 6f;
        float plotBottom = dirtyRect.Bottom - labelHeight;
        float plotHeight = plotBottom - plotTop;
        if (plotHeight <= 0 || plotRight <= plotLeft)
            return;

        double max = 0;
        foreach (var p in items)
        {
            if ((double)p.Spending.Amount > max) max = (double)p.Spending.Amount;
            if ((double)p.Earning.Amount > max) max = (double)p.Earning.Amount;
        }

        // Nice-number ceiling so the axis tracks real data without jumping wildly.
        var axisMax = NiceCeiling(max);
        if (axisMax <= 0) axisMax = 1;

        canvas.FontSize = 8;
        var gridColor = FinoraOverlay.Resolve("FinoraCreamDeep", "#E3E8D5");
        for (var step = 0; step <= 4; step++)
        {
            var value = axisMax * step / 4;
            var y = plotBottom - (float)(value / axisMax * plotHeight);
            canvas.StrokeColor = gridColor;
            canvas.StrokeSize = 1;
            canvas.DrawLine(plotLeft, y, plotRight, y);
            canvas.FillColor = FinoraOverlay.Resolve("FinoraMuted", "#6F7668");
            canvas.DrawString(Compact(value), dirtyRect.Left, y - 6, axisWidth - 4, 14, HorizontalAlignment.Right, VerticalAlignment.Center);
        }

        int count = items.Count;
        float groupWidth = (plotRight - plotLeft) / count;
        float barWidth = MathF.Min(14f, MathF.Max(3f, (groupWidth - 8f) / 2f));

        canvas.FontSize = 9;
        for (int i = 0; i < count; i++)
        {
            var p = items[i];
            float groupX = plotLeft + i * groupWidth;
            float cx = groupX + groupWidth / 2f;

            float spendingHeight = (float)((double)p.Spending.Amount / axisMax * plotHeight);
            float earningHeight = (float)((double)p.Earning.Amount / axisMax * plotHeight);

            canvas.FillColor = SpendingColor;
            canvas.FillRoundedRectangle(cx - barWidth - 1f, plotBottom - spendingHeight, barWidth, spendingHeight, 3f);
            canvas.FillColor = EarningColor;
            canvas.FillRoundedRectangle(cx + 1f, plotBottom - earningHeight, barWidth, earningHeight, 3f);

            if (groupWidth >= 26f || i % Math.Max(1, (int)(26 / MathF.Max(1f, groupWidth))) == 0)
            {
                canvas.FillColor = i == SelectedIndex ? FinoraOverlay.Resolve("FinoraInk", "#161B16") : FinoraOverlay.Resolve("FinoraMuted", "#6F7668");
                canvas.DrawString(p.Label, groupX, plotBottom + 4f, groupWidth, labelHeight - 4f,
                    HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }
    }

    private static double NiceCeiling(double value)
    {
        if (value <= 0) return 0;
        var exponent = Math.Floor(Math.Log10(value));
        var fraction = value / Math.Pow(10, exponent);
        var nice = fraction switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 2.5 => 2.5,
            <= 5 => 5,
            _ => 10
        };
        return nice * Math.Pow(10, exponent);
    }

    private static string Compact(double value)
    {
        if (value >= 1_000_000) return $"{value / 1_000_000:0.#}M";
        if (value >= 1_000) return $"{value / 1_000:0.#}K";
        return value.ToString("0");
    }
}
