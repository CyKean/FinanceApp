namespace FinanceApp.Mobile.Helpers;

/// <summary>
/// Entrance animations: children of a layout fade + slide up one after another.
/// </summary>
public static class PageAnimator
{
    private const uint DurationMs = 420;
    private const int StaggerMs = 60;
    private const int MaxAnimated = 8;

    public static async Task StaggerInAsync(Layout layout)
    {
        var children = layout.Children.OfType<VisualElement>().Take(MaxAnimated).ToList();

        foreach (var child in children)
        {
            child.Opacity = 0;
            child.TranslationY = 28;
        }

        var tasks = new List<Task>();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var delay = i * StaggerMs;
            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(delay);
                await MainThread.InvokeOnMainThreadAsync(() => Task.WhenAll(
                    child.FadeToAsync(1, DurationMs, Easing.CubicOut),
                    child.TranslateToAsync(0, 0, DurationMs, Easing.CubicOut)));
            }));
        }

        await Task.WhenAll(tasks);
    }
}
