namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Application.DTOs;

public class CategoryDonutChart : GraphicsView
{
    public static readonly BindableProperty ItemsProperty =
        BindableProperty.Create(nameof(Items), typeof(IReadOnlyList<CategorySpendingDto>), typeof(CategoryDonutChart), null,
            propertyChanged: (b, _, newValue) => ((CategoryDonutChart)b).OnItemsChanged(newValue));

    public IReadOnlyList<CategorySpendingDto>? Items
    {
        get => (IReadOnlyList<CategorySpendingDto>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private readonly CategoryDonutDrawable _drawable = new();

    public CategoryDonutChart()
    {
        Drawable = _drawable;
    }

    private void OnItemsChanged(object? newValue)
    {
        _drawable.Items = newValue as IReadOnlyList<CategorySpendingDto>;
        Invalidate();
    }
}

internal sealed class CategoryDonutDrawable : IDrawable
{
    public IReadOnlyList<CategorySpendingDto>? Items { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float size = MathF.Min(dirtyRect.Width, dirtyRect.Height);
        if (size <= 0)
            return;

        float cx = dirtyRect.Left + dirtyRect.Width / 2;
        float cy = dirtyRect.Top + dirtyRect.Height / 2;
        float outer = size / 2f - 2f;
        float inner = outer * 0.64f;

        var items = Items;
        decimal total = 0;
        if (items != null)
        {
            foreach (var item in items)
                total += item.Amount.Amount;
        }

        if (total <= 0 || items == null)
        {
            canvas.FillColor = ThemeResources.GetColor("OutlineVariant", "OutlineVariantDark");
            canvas.FillPath(BuildRing(cx, cy, outer, inner, -90f, 269f));
            return;
        }

        float start = -90f;
        foreach (var item in items)
        {
            if (item.Amount.Amount <= 0)
                continue;

            float sweep = (float)(item.Amount.Amount / total) * 360f;
            float gap = sweep > 8f ? 2f : 0f;
            canvas.FillColor = ParseColor(item.CategoryColor);
            canvas.FillPath(BuildRing(cx, cy, outer, inner, start + gap / 2f, start + sweep - gap / 2f));
            start += sweep;
        }

        var currency = items.Count > 0 ? items[0].Amount.Currency : string.Empty;
        canvas.FontColor = ThemeResources.GetColor("OnSurfaceVariant", "OnSurfaceVariantDark");
        canvas.FontSize = 11;
        canvas.DrawString("Spent",
            cx - inner, cy - 24f, inner * 2f, 16f,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        canvas.FontColor = ThemeResources.GetColor("OnSurface", "OnSurfaceDark");
        canvas.FontSize = 14;
        var totalText = total.ToString("N2") + (string.IsNullOrEmpty(currency) ? string.Empty : " " + currency);
        canvas.DrawString(totalText,
            cx - inner, cy - 6f, inner * 2f, 20f,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static PathF BuildRing(float cx, float cy, float outer, float inner, float startDeg, float endDeg)
    {
        var path = new PathF();
        if (endDeg <= startDeg)
            return path;

        int steps = Math.Max(4, (int)MathF.Ceiling((endDeg - startDeg) / 3f));
        for (int i = 0; i <= steps; i++)
        {
            float rad = (startDeg + (endDeg - startDeg) * i / steps) * MathF.PI / 180f;
            float x = cx + outer * MathF.Cos(rad);
            float y = cy + outer * MathF.Sin(rad);
            if (i == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
        }

        for (int i = steps; i >= 0; i--)
        {
            float rad = (startDeg + (endDeg - startDeg) * i / steps) * MathF.PI / 180f;
            path.LineTo(cx + inner * MathF.Cos(rad), cy + inner * MathF.Sin(rad));
        }

        path.Close();
        return path;
    }

    private static Color ParseColor(string? hex)
    {
        try
        {
            return Color.FromArgb(hex ?? string.Empty);
        }
        catch
        {
            return Color.FromArgb("#0E6B4F");
        }
    }
}
