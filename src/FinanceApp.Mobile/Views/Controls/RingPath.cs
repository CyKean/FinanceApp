namespace FinanceApp.Mobile.Views.Controls;

internal static class RingPath
{
    public static PathF Build(float cx, float cy, float outer, float inner, float startDeg, float endDeg)
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
}
