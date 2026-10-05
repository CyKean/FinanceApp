namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Failure prompt: a red badge with the warning glyph and a single acknowledge
/// button. Failures are acknowledged, not decided, so there is no cancel.
/// </summary>
public partial class ErrorModalHost : ContentView
{
    private ErrorModalService? _errorModalService;
    private ErrorModalRequest? _active;

    public ErrorModalHost()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _errorModalService = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetService<ErrorModalService>();
        if (_errorModalService != null)
            _errorModalService.ErrorRequested += OnErrorRequested;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_errorModalService != null)
            _errorModalService.ErrorRequested -= OnErrorRequested;
        _errorModalService = null;
    }

    private void OnErrorRequested(ErrorModalRequest request) =>
        MainThread.BeginInvokeOnMainThread(() => Show(request));

    private void Show(ErrorModalRequest request)
    {
        if (_active != null)
        {
            var previous = _active;
            _active = null;
            previous.Completion.TrySetResult(false);
        }

        _active = request;

        TitleLabel.Text = request.Title;
        MessageLabel.Text = request.Message;

        var (accent, onAccent, glyph) = FinoraOverlay.ForResult(failed: true);
        IconBadge.BackgroundColor = accent;
        Glyph.Stroke = onAccent;
        Glyph.Data = FinoraIcons.GetGeometry(glyph);

        IsVisible = true;
        Scrim.Opacity = 0;
        Card.Opacity = 0;
        Card.Scale = FinoraOverlay.CardScaleIn;

        _ = Scrim.FadeToAsync(1, FinoraOverlay.ScrimFadeMs, Easing.CubicOut);
        _ = Card.FadeToAsync(1, FinoraOverlay.CardInMs, Easing.CubicOut);
        _ = Card.ScaleToAsync(1, FinoraOverlay.SpringMs, Easing.SpringOut);
    }

    private void OnCancelTapped(object? sender, EventArgs e)
    {
        var active = _active;
        if (active == null) return;

        _active = null;
        _ = HideAsync();
        active.Completion.TrySetResult(true);
    }

    private async Task HideAsync()
    {
        await Task.WhenAll(
            Scrim.FadeToAsync(0, FinoraOverlay.CardOutMs, Easing.CubicIn),
            Card.FadeToAsync(0, FinoraOverlay.CardOutMs, Easing.CubicIn),
            Card.ScaleToAsync(FinoraOverlay.CardScaleOut, FinoraOverlay.CardOutMs, Easing.CubicIn));
        IsVisible = false;
    }
}