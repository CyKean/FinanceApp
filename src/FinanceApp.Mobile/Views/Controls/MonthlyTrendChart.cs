namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Application.DTOs;

public class MonthlyTrendChart : GraphicsView
{
    public static readonly BindableProperty ItemsProperty =
        BindableProperty.Create(nameof(Items), typeof(IReadOnlyList<MonthlyTrendDto>), typeof(MonthlyTrendChart), null,
            propertyChanged: (b, _, newValue) => ((MonthlyTrendChart)b).OnItemsChanged(newValue));

    public IReadOnlyList<MonthlyTrendDto>? Items
    {
        get => (IReadOnlyList<MonthlyTrendDto>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private readonly MonthlyTrendDrawable _drawable = new();

    public MonthlyTrendChart()
    {
        Drawable = _drawable;
    }

    private void OnItemsChanged(object? newValue)
    {
        _drawable.Items = newValue as IReadOnlyList<MonthlyTrendDto>;
        Invalidate();
    }
}

internal sealed class MonthlyTrendDrawable : IDrawable
{
    private static Color IncomeColor => FinoraOverlay.Resolve("Success", "#15803D");
    private static Color ExpenseColor => FinoraOverlay.Resolve("Error", "#DC2626");

    public IReadOnlyList<MonthlyTrendDto>? Items { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var items = Items;
        if (items == null || items.Count == 0 || dirtyRect.Height <= 0)
            return;

        const float labelHeight = 22f;
        float chartTop = dirtyRect.Top + 6f;
        float chartBottom = dirtyRect.Bottom - labelHeight;
        float chartHeight = chartBottom - chartTop;
        if (chartHeight <= 0)
            return;

        double max = 0;
        foreach (var trend in items)
        {
            if ((double)trend.Income.Amount > max) max = (double)trend.Income.Amount;
            if ((double)trend.Expense.Amount > max) max = (double)trend.Expense.Amount;
        }
        if (max <= 0) max = 1;

        int count = items.Count;
        float groupWidth = dirtyRect.Width / count;
        float outerGap = MathF.Min(10f, groupWidth * 0.14f);
        float barWidth = MathF.Max(2f, (groupWidth - outerGap * 2f - 4f) / 2f);
        float radius = MathF.Min(4f, barWidth / 2f);

        canvas.FontSize = 10;
        canvas.FontColor = ThemeResources.GetColor("OnSurfaceVariant", "OnSurfaceVariantDark");

        for (int i = 0; i < count; i++)
        {
            var trend = items[i];
            float groupX = dirtyRect.Left + i * groupWidth;
            float incomeHeight = (float)((double)trend.Income.Amount / max * chartHeight);
            float expenseHeight = (float)((double)trend.Expense.Amount / max * chartHeight);

            canvas.FillColor = IncomeColor;
            canvas.FillRoundedRectangle(groupX + outerGap, chartBottom - incomeHeight, barWidth, incomeHeight, radius);

            canvas.FillColor = ExpenseColor;
            canvas.FillRoundedRectangle(groupX + outerGap + barWidth + 4f, chartBottom - expenseHeight, barWidth, expenseHeight, radius);

            if (groupWidth >= 30f || i % 2 == 0)
            {
                canvas.DrawString(trend.Date.ToString("MMM"),
                    groupX, chartBottom + 4f, groupWidth, labelHeight - 4f,
                    HorizontalAlignment.Center, VerticalAlignment.Top);
            }
        }

        canvas.StrokeColor = ThemeResources.GetColor("OutlineVariant", "OutlineVariantDark");
        canvas.StrokeSize = 1;
        canvas.DrawLine(dirtyRect.Left, chartBottom + 1, dirtyRect.Right, chartBottom + 1);
    }
}
