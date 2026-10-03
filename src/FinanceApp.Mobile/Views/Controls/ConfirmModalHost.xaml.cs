namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Yes/no prompt. Destructive actions get a red badge with a warning glyph;
/// everything else uses the lime tick, matching the rest of the Finora theme.
/// </summary>
public partial class ConfirmModalHost : ContentView
{
    private ConfirmModalService? _confirmModalService;
    private ConfirmModalRequest? _active;

    public ConfirmModalHost()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _confirmModalService = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetService<ConfirmModalService>();
        if (_confirmModalService != null)
            _confirmModalService.ConfirmRequested += OnConfirmRequested;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_confirmModalService != null)
            _confirmModalService.ConfirmRequested -= OnConfirmRequested;
        _confirmModalService = null;
    }

    private void OnConfirmRequested(ConfirmModalRequest request) =>
        MainThread.BeginInvokeOnMainThread(() => Show(request));

    private void Show(ConfirmModalRequest request)
    {
        // A new prompt supersedes the old one: settle it so its awaiter returns.
        if (_active != null)
        {
            var previous = _active;
            _active = null;
            previous.Completion.TrySetResult(false);
        }

        _active = request;

        TitleLabel.Text = request.Title;
        MessageLabel.Text = request.Message;
        CancelButton.Text = string.IsNullOrWhiteSpace(request.CancelText) ? "Cancel" : request.CancelText;
        ConfirmButton.Text = string.IsNullOrWhiteSpace(request.ConfirmText) ? "Confirm" : request.ConfirmText;

        var (accent, onAccent, glyph) = FinoraOverlay.ForConfirm(request.IsDestructive);
        IconBadge.BackgroundColor = accent;
        Glyph.Stroke = onAccent;
        Glyph.Data = FinoraIcons.GetGeometry(glyph);

        // A destructive confirm has to read as destructive, so it keeps the
        // Danger style; everything else uses the primary ink button.
        ConfirmButton.Style = ResolveStyle(request.IsDestructive ? "DangerButtonStyle" : "PrimaryButtonStyle");

        IsVisible = true;
        Scrim.Opacity = 0;
        Card.Opacity = 0;
        Card.Scale = FinoraOverlay.CardScaleIn;

        _ = Scrim.FadeToAsync(1, FinoraOverlay.ScrimFadeMs, Easing.CubicOut);
        _ = Card.FadeToAsync(1, FinoraOverlay.CardInMs, Easing.CubicOut);
        _ = Card.ScaleToAsync(1, FinoraOverlay.SpringMs, Easing.SpringOut);
    }

    private void OnCancelTapped(object? sender, EventArgs e) => Complete(false);

    private void OnConfirmTapped(object? sender, EventArgs e) => Complete(true);

    private void Complete(bool result)
    {
        var active = _active;
        if (active == null) return;

        _active = null;
        _ = HideAsync();
        active.Completion.TrySetResult(result);
    }

    private async Task HideAsync()
    {
        await Task.WhenAll(
            Scrim.FadeToAsync(0, FinoraOverlay.CardOutMs, Easing.CubicIn),
            Card.FadeToAsync(0, FinoraOverlay.CardOutMs, Easing.CubicIn),
            Card.ScaleToAsync(FinoraOverlay.CardScaleOut, FinoraOverlay.CardOutMs, Easing.CubicIn));
        IsVisible = false;
    }

    private static Style? ResolveStyle(string key)
    {
        var resources = Microsoft.Maui.Controls.Application.Current?.Resources;
        if (resources == null) return null;
        return resources.TryGetValue(key, out var value) ? value as Style : null;
    }
}
