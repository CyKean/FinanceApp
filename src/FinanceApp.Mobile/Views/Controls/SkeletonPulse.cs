namespace FinanceApp.Mobile.Views.Controls;

/// <summary>
/// The single pulse implementation shared by every placeholder in the app.
/// <para>
/// The important part is the <c>repeat</c> callback. MAUI re-evaluates it after
/// every cycle, so a placeholder that has just been hidden stops on its own
/// within one cycle. It used to be <c>() => true</c>, which meant the animation
/// outlived its page: the platform ticker never checks visibility, so a page the
/// user had visited once kept posting 60fps redraws of a hidden ~60-view
/// placeholder tree for the rest of the navigation stack's life - on a device
/// that cannot afford to lose those frames.
/// </para>
/// <para>
/// <see cref="Stop"/> on top of that is belt and braces: it fires on handler
/// teardown, so a popped page releases its ticker immediately instead of waiting
/// out the cycle in which it would notice.
/// </para>
/// </summary>
internal static class SkeletonPulse
{
    public const string Name = "skeleton-pulse";

    public static void Start(VisualElement owner)
    {
        if (owner is null)
            return;

        owner.AbortAnimation(Name);

        // A placeholder restored from the back stack starts transparent-ready:
        // without the reset, a pulse interrupted at 0.55 leaves it dim forever.
        if (!owner.IsVisible)
        {
            owner.Opacity = 1;
            return;
        }

        var animation = new Animation(value => owner.Opacity = value, 0.55, 1.0, Easing.CubicInOut);

        animation.Commit(
            owner,
            Name,
            length: 850,
            easing: Easing.SinInOut,
            finished: null,
            repeat: () => owner.IsVisible);
    }

    public static void Stop(VisualElement? owner) => owner?.AbortAnimation(Name);
}