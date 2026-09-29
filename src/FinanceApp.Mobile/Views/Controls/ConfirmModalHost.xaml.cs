namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

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

        var accent = request.IsDestructive ? "#DC2626" : "#0E6B4F";
        IconBadge.BackgroundColor = Color.FromArgb(accent);
        IconLabel.Text = request.IsDestructive ? "\U0001F5D1" : "\u2713";

        var style = ResolveStyle(request.IsDestructive ? "DangerButtonStyle" : "PrimaryButtonStyle");
        if (style != null)
            ConfirmButton.Style = style;

        IsVisible = true;
        Scrim.Opacity = 0;
        Card.Opacity = 0;
        Card.Scale = 0.92;
        _ = Scrim.FadeToAsync(1, 160);
        _ = Card.FadeToAsync(1, 160);
        _ = Card.ScaleToAsync(1, 160);
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
            Scrim.FadeToAsync(0, 140),
            Card.FadeToAsync(0, 140),
            Card.ScaleToAsync(0.94, 140));
        IsVisible = false;
    }

    private static Style? ResolveStyle(string key)
    {
        var resources = Microsoft.Maui.Controls.Application.Current?.Resources;
        if (resources == null) return null;
        return resources.TryGetValue(key, out var value) ? value as Style : null;
    }
}
