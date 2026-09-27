namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Dispatching;

/// <summary>
/// Renders <see cref="ToastService"/> requests as a bottom toast card.
/// Drop as the last child of any page-level Grid. pointer input passes through.
/// </summary>
public partial class ToastHost : ContentView
{
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

    private void OnToastRequested(ToastRequest request)
    {
        MainThread.BeginInvokeOnMainThread(() => Show(request));
    }

    private void Show(ToastRequest request)
    {
        _hideTimer?.Stop();

        var (accent, glyph) = request.Kind switch
        {
            ToastKind.Error => ("#DC2626", "✕"),
            ToastKind.Info => ("#0284C7", "i"),
            _ => ("#16A34A", "✓")
        };

        IconBadge.BackgroundColor = Color.FromArgb(accent);
        IconLabel.Text = glyph;
        MessageLabel.Text = request.Message;

        IsVisible = true;
        Opacity = 0;
        this.FadeTo(1, 150);

        _hideTimer = Dispatcher.CreateTimer();
        _hideTimer.Interval = TimeSpan.FromSeconds(2.6);
        _hideTimer.IsRepeating = false;
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer?.Stop();
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await this.FadeTo(0, 200);
                IsVisible = false;
            });
        };
        _hideTimer.Start();
    }
}
