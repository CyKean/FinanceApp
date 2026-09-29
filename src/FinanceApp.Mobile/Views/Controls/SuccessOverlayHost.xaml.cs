namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

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
            var previous = _active;
            _active = request;
            previous?.Completion.TrySetResult(true);
            _ = PlayAsync(request);
        });
    }

    private async Task PlayAsync(SuccessAnimationRequest request)
    {
        MessageLabel.Text = request.Message;

        Scrim.Opacity = 0;
        Card.Opacity = 0;
        Card.Scale = 0.65;
        Badge.Opacity = 0;
        Badge.Scale = 0.3;
        CheckLabel.Opacity = 0;
        CheckLabel.Scale = 0.3;
        MessageLabel.Opacity = 0;
        IsVisible = true;

        _ = Scrim.FadeToAsync(1, 160);
        await Task.WhenAll(Card.FadeToAsync(1, 220), Card.ScaleToAsync(1, 320, Easing.SpringOut));
        await Task.WhenAll(Badge.FadeToAsync(1, 160), Badge.ScaleToAsync(1, 380, Easing.SpringOut));
        await Task.WhenAll(CheckLabel.FadeToAsync(1, 180), CheckLabel.ScaleToAsync(1, 320, Easing.SpringOut));
        _ = MessageLabel.FadeToAsync(1, 200);

        await Task.Delay(800);

        if (!ReferenceEquals(_active, request))
            return;

        await Task.WhenAll(
            Scrim.FadeToAsync(0, 220),
            Card.FadeToAsync(0, 220),
            Card.ScaleToAsync(0.9, 220));

        IsVisible = false;
        _active = null;
        request.Completion.TrySetResult(true);
    }
}
