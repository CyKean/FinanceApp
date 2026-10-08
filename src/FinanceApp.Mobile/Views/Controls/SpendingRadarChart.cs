namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Application.DTOs;

/// <summary>
/// Spending radar driven by <see cref="SpendingSliceDto"/> slices from the
/// statistics pipeline. Each axis is one account type, and the polygon vertex
/// on each axis is proportional to that account type's share of spending.
/// </summary>
public class SpendingRadarChart : GraphicsView
{
    public static readonly BindableProperty ItemsProperty =
        BindableProperty.Create(nameof(Items), typeof(IReadOnlyList<SpendingSliceDto>), typeof(SpendingRadarChart), null,
            propertyChanged: (b, _, v) => ((SpendingRadarChart)b).OnItemsChanged(v));

    public IReadOnlyList<SpendingSliceDto>? Items
    {
        get => (IReadOnlyList<SpendingSliceDto>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private readonly SpendingRadarDrawable _drawable = new();

    public SpendingRadarChart()
    {
        Drawable = _drawable;
    }

    private void OnItemsChanged(object? newValue)
    {
        _drawable.Items = newValue as IReadOnlyList<SpendingSliceDto>;
        Invalidate();
    }
}

internal sealed class SpendingRadarDrawable : IDrawable
{
    public IReadOnlyList<SpendingSliceDto>? Items { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var items = Items;
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
            return;

        var centerX = dirtyRect.Center.X;
        var centerY = dirtyRect.Center.Y;
        var radius = MathF.Min(dirtyRect.Width, dirtyRect.Height) / 2f - 18f;
        if (radius <= 0)
            return;

        var axes = items is not null && items.Count >= 3 ? items.Count : 0;

        if (axes == 0)
        {
            // Empty-state: draw a flat reference polygon so the chart area does
            // not collapse into nothing, and a subtle marker that no data exists.
            DrawReferencePolygon(canvas, centerX, centerY, radius, 5);
            canvas.FillColor = ThemeResources.GetColor("OnSurfaceVariant", "OnSurfaceVariantDark");
            canvas.FontSize = 9;
            canvas.DrawString("No spending data", dirtyRect.Left, centerY - 7, dirtyRect.Width, 14,
                HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        DrawReferencePolygon(canvas, centerX, centerY, radius, axes);

        var max = items!.Max(i => i.Amount.Amount);
        if (max <= 0) max = 1;

        var points = new List<PointF>(axes);
        for (var i = 0; i < axes; i++)
        {
            var angle = -Math.PI / 2 + i * 2 * Math.PI / axes;
            var r = (float)((double)(items[i].Amount.Amount / max)) * radius;
            points.Add(new PointF(centerX + r * (float)Math.Cos(angle), centerY + r * (float)Math.Sin(angle)));
        }

        var path = new PathF();
        path.MoveTo(points[0].X, points[0].Y);
        for (var i = 1; i < points.Count; i++)
            path.LineTo(points[i].X, points[i].Y);
        path.Close();

        canvas.FillColor = FinoraOverlay.Resolve("FinoraLime", "#CDF463").WithAlpha(0.35f);
        canvas.FillPath(path);
        canvas.StrokeColor = FinoraOverlay.Resolve("FinoraLime", "#CDF463");
        canvas.StrokeSize = 1.5f;
        canvas.DrawPath(path);

        canvas.FontSize = 8;
        for (var i = 0; i < axes; i++)
        {
            var angle = -Math.PI / 2 + i * 2 * Math.PI / axes;
            var lx = centerX + (radius + 10f) * (float)Math.Cos(angle);
            var ly = centerY + (radius + 10f) * (float)Math.Sin(angle);
            canvas.FillColor = ThemeResources.GetColor("OnSurfaceVariant", "OnSurfaceVariantDark");
            canvas.DrawString(items[i].Label, lx - 30f, ly - 7f, 60f, 14f,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    private static void DrawReferencePolygon(ICanvas canvas, float cx, float cy, float radius, int axes)
    {
        for (var ring = 1; ring <= 3; ring++)
        {
            var r = radius * ring / 3f;
            var path = new PathF();
            for (var i = 0; i < axes; i++)
            {
                var angle = -Math.PI / 2 + i * 2 * Math.PI / axes;
                var x = cx + r * (float)Math.Cos(angle);
                var y = cy + r * (float)Math.Sin(angle);
                if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
            }
            path.Close();
            canvas.StrokeColor = FinoraOverlay.Resolve("OutlineVariant", "#3A4A3A");
            canvas.StrokeSize = 1f;
            canvas.DrawPath(path);
        }
    }
}
