namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Full-screen confirmation for a save: a lime tick for success, a red alert for
/// failure (with a shake), plus a Lucide glyph instead of a text character.
/// </summary>
public partial class SuccessOverlayHost : ContentView
{
    private SuccessAnimationService? _successService;
    private SuccessAnimationRequest? _active;

    public SuccessOverlayHost()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _successService = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetService<SuccessAnimationService>();
        if (_successService != null)
            _successService.SuccessRequested += OnSuccessRequested;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_successService != null)
            _successService.SuccessRequested -= OnSuccessRequested;
        _successService = null;
    }

    private void OnSuccessRequested(SuccessAnimationRequest request)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // A second request supersedes the first: settle the old one so its
            // awaiter is not left hanging.
            var previous = _active;
            _active = request;
            previous?.Completion.TrySetResult(true);
            _ = PlayAsync(request);
        });
    }

    private async Task PlayAsync(SuccessAnimationRequest request)
    {
        var isFailure = request.Kind == AnimationKind.Failure;
        var (accent, onAccent, glyph) = PaytinOverlay.ForResult(isFailure);

        Badge.BackgroundColor = accent;
        Glyph.Stroke = onAccent;
        Glyph.Data = PaytinIcons.GetGeometry(glyph);
        MessageLabel.Text = request.Message;

        Card.Opacity = 0;
        Card.Scale = PaytinOverlay.CardScaleIn;
        Card.TranslationX = 0;
        Scrim.Opacity = 0;
        Badge.Opacity = 0;
        Badge.Scale = 0.4;
        Badge.Rotation = isFailure ? -14 : 0;
        Glyph.Opacity = 0;
        Glyph.Scale = 0.4;
        MessageLabel.Opacity = 0;
        IsVisible = true;

        _ = Scrim.FadeToAsync(1, PaytinOverlay.ScrimFadeMs, Easing.CubicOut);
        await Task.WhenAll(
            Card.FadeToAsync(1, PaytinOverlay.CardInMs, Easing.CubicOut),
            Card.ScaleToAsync(1, PaytinOverlay.SpringMs, Easing.SpringOut));
        await Task.WhenAll(
            Badge.FadeToAsync(1, PaytinOverlay.ScrimFadeMs, Easing.CubicOut),
            Badge.ScaleToAsync(1, PaytinOverlay.SpringMs, Easing.SpringOut));
        await Task.WhenAll(
            Glyph.FadeToAsync(1, PaytinOverlay.CardInMs, Easing.CubicOut),
            Glyph.ScaleToAsync(1, PaytinOverlay.SpringMs, Easing.SpringOut));
        _ = MessageLabel.FadeToAsync(1, PaytinOverlay.CardInMs, Easing.CubicOut);

        if (isFailure)
            await ShakeAsync();

        await Task.Delay(isFailure ? PaytinOverlay.FailureHoldDuration : PaytinOverlay.HoldDuration);

        // A newer overlay may have taken over while this one was on screen.
        if (!ReferenceEquals(_active, request))
            return;

        await Task.WhenAll(
            Scrim.FadeToAsync(0, PaytinOverlay.CardOutMs, Easing.CubicIn),
            Card.FadeToAsync(0, PaytinOverlay.CardOutMs, Easing.CubicIn),
            Card.ScaleToAsync(PaytinOverlay.CardScaleOut, PaytinOverlay.CardOutMs, Easing.CubicIn));

        IsVisible = false;
        _active = null;
        request.Completion.TrySetResult(true);
    }

    private async Task ShakeAsync()
    {
        await Card.TranslateToAsync(-14, 0, 70, Easing.CubicOut);
        await Card.TranslateToAsync(14, 0, 110, Easing.Linear);
        await Card.TranslateToAsync(-9, 0, 110, Easing.Linear);
        await Card.TranslateToAsync(9, 0, 110, Easing.Linear);
        await Card.TranslateToAsync(-4, 0, 90, Easing.Linear);
        await Card.TranslateToAsync(0, 0, 90, Easing.CubicIn);
    }
}
