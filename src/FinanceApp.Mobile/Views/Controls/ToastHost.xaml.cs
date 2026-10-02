namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Renders <see cref="ToastService"/> requests as a bottom toast card.
/// Drop as the last child of any page-level Grid; pointer input passes through.
/// </summary>
public partial class ToastHost : ContentView
{
    private const double SlideIn = 24;

    private IDispatcherTimer? _hideTimer;
    private ToastService? _toastService;

    public ToastHost()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _toastService = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetService<ToastService>();
        if (_toastService != null)
            _toastService.ToastRequested += OnToastRequested;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_toastService != null)
            _toastService.ToastRequested -= OnToastRequested;
        _toastService = null;
    }

    private void OnToastRequested(ToastRequest request) =>
        MainThread.BeginInvokeOnMainThread(() => Show(request));

    private void Show(ToastRequest request)
    {
        _hideTimer?.Stop();

        var (accent, onAccent, glyph) = PaytinOverlay.ForToast(request.Kind);
        IconBadge.BackgroundColor = accent;
        Glyph.Stroke = onAccent;
        Glyph.Data = PaytinIcons.GetGeometry(glyph);
        MessageLabel.Text = request.Message;

        IsVisible = true;
        Opacity = 0;
        ToastRow.TranslationY = SlideIn;
        ToastRow.Scale = 0.96;

        _ = Task.WhenAll(
            this.FadeToAsync(1, PaytinOverlay.ToastInMs, Easing.CubicOut),
            ToastRow.TranslateToAsync(0, 0, PaytinOverlay.ToastInMs, Easing.CubicOut),
            ToastRow.ScaleToAsync(1, PaytinOverlay.ToastInMs, Easing.CubicOut));

        _hideTimer = Dispatcher.CreateTimer();
        _hideTimer.Interval = PaytinOverlay.ToastDuration;
        _hideTimer.IsRepeating = false;
        _hideTimer.Tick += OnHideTimerTicked;
        _hideTimer.Start();
    }

    private void OnHideTimerTicked(object? sender, EventArgs e)
    {
        _hideTimer?.Stop();
        _ = HideAsync();
    }

    private async Task HideAsync()
    {
        // Guard against a newer toast having taken over mid-hide.
        if (!IsVisible)
            return;

        await Task.WhenAll(
            this.FadeToAsync(0, PaytinOverlay.ToastOutMs, Easing.CubicIn),
            ToastRow.TranslateToAsync(0, SlideIn, PaytinOverlay.ToastOutMs, Easing.CubicIn),
            ToastRow.ScaleToAsync(PaytinOverlay.CardScaleOut, PaytinOverlay.ToastOutMs, Easing.CubicIn));

        IsVisible = false;
    }
}
