namespace FinanceApp.Mobile.Helpers;

public static class DeletionAnimator
{
    private const uint DurationMs = 280;

    public static Func<object, Task> Create(VisualElement root) =>
        item => AnimateAsync(root, item);

    private static async Task AnimateAsync(VisualElement root, object item)
    {
        var container = FindContainer(root, item);
        if (container == null)
            return;

        await Task.WhenAll(
            container.FadeToAsync(0, DurationMs, Easing.CubicIn),
            container.ScaleToAsync(0.85, DurationMs, Easing.CubicIn),
            container.TranslateToAsync(-90, 0, DurationMs, Easing.CubicIn));
    }

    private static SwipeView? FindContainer(VisualElement root, object item)
    {
        foreach (var element in EnumerateVisualTree(root))
        {
            if (element is SwipeView swipe && ReferenceEquals(swipe.BindingContext, item))
                return swipe;
        }

        return null;
    }

    private static IEnumerable<Element> EnumerateVisualTree(Element root)
    {
        if (root is not IVisualTreeElement node)
            yield break;

        foreach (var child in node.GetVisualChildren())
        {
            if (child is not Element element)
                continue;

            yield return element;

            foreach (var descendant in EnumerateVisualTree(element))
                yield return descendant;
        }
    }
}
