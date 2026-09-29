namespace FinanceApp.Mobile.Views.Controls;

internal sealed record BarRow(string Label, float Fraction, string Value, Color Fill);

internal static class BarRowsPainter
{
    public static void Draw(ICanvas canvas, RectF bounds, IReadOnlyList<BarRow> rows)
    {
        if (rows.Count == 0 || bounds.Height <= 0)
            return;

        const float labelWidth = 70f;
        const float valueWidth = 88f;
        const float barHeight = 14f;
        const float rowStep = 30f;

        float trackLeft = bounds.Left + labelWidth;
        float trackWidth = bounds.Width - labelWidth - valueWidth;
        if (trackWidth < 30f)
            trackWidth = 30f;

        var trackColor = ThemeResources.GetColor("OutlineVariant", "OutlineVariantDark");
        var labelColor = ThemeResources.GetColor("OnSurfaceVariant", "OnSurfaceVariantDark");
        var valueColor = ThemeResources.GetColor("OnSurface", "OnSurfaceDark");

        float y = bounds.Top + 2f;
        foreach (var row in rows)
        {
            canvas.FontSize = 11;
            canvas.FontColor = labelColor;
            canvas.DrawString(row.Label, bounds.Left, y - 4f, labelWidth - 8f, barHeight + 8f,
                HorizontalAlignment.Left, VerticalAlignment.Center);

            canvas.FillColor = trackColor;
            canvas.FillRoundedRectangle(trackLeft, y, trackWidth, barHeight, barHeight / 2f);

            float fraction = Math.Clamp(row.Fraction, 0f, 1f);
            if (fraction > 0f)
            {
                float fillWidth = Math.Max(4f, fraction * trackWidth);
                canvas.FillColor = row.Fill;
                canvas.FillRoundedRectangle(trackLeft, y, fillWidth, barHeight, barHeight / 2f);
            }

            canvas.FontSize = 11;
            canvas.FontColor = valueColor;
            canvas.DrawString(row.Value, trackLeft + trackWidth + 8f, y - 4f, valueWidth - 8f, barHeight + 8f,
                HorizontalAlignment.Right, VerticalAlignment.Center);

            y += rowStep;
        }
    }
}
