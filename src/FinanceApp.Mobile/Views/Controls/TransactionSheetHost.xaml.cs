namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Domain.Enums;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;
using Microsoft.Extensions.DependencyInjection;

public partial class TransactionSheetHost : ContentView, ITransactionSheetHost
{
    private const uint EnterMs = 320;
    private const uint ExitMs = 220;
    private const uint FadeMs = 200;

    /// <summary>Share of the page the form may occupy before it starts scrolling.</summary>
    private const double MaxSheetFraction = 0.78;

    /// <summary>Sheet chrome that sits outside the scrolling form (handle + padding).</summary>
    private const double SheetChrome = 46;

    private const double FallbackPageHeight = 800;

    private TransactionSheetService? _service;
    private AddTransactionViewModel? _viewModel;
    private bool _isOpen;

    public TransactionSheetHost()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        var services = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services;
        if (services is null)
            return;

        _service = services.GetService<TransactionSheetService>();
        _viewModel = services.GetService<AddTransactionViewModel>();

        if (_service is null || _viewModel is null)
            return;

        BindingContext = _viewModel;

        // The host closes itself instead of navigating, so the page behind
        // stays put and the exit animation can finish first.
        _viewModel.OnSavedCallback = () => DismissAsync(saved: true);
        _viewModel.OnCancelledCallback = () => DismissAsync(saved: false);

        _service.Register(this);
        ClampSheetHeight();
    }

    private void OnUnloaded(object? sender, EventArgs e) => _service?.Unregister(this);

    private void OnSizeChanged(object? sender, EventArgs e) => ClampSheetHeight();

    public void HandleShow(TransactionType type, Guid? transactionId = null) =>
        MainThread.BeginInvokeOnMainThread(() => _ = ShowAsync(type, transactionId));

    private async Task ShowAsync(TransactionType type, Guid? transactionId = null)
    {
        if (_viewModel is null || _isOpen)
            return;

        await _viewModel.InitializeAsync(type, transactionId);

        _isOpen = true;
        IsVisible = true;

        // Clamp before measuring: while the host was hidden SizeChanged never
        // fired, so without this the form grows to its full content height and
        // overflows the page.
        ClampSheetHeight();

        // Let the layout pass run so the sheet has a real height to slide from.
        await Task.Yield();

        Sheet.Opacity = 0;
        Sheet.TranslationY = SlideDistance();
        Scrim.Opacity = 0;

        await Task.WhenAll(
            Sheet.TranslateToAsync(0, 0, EnterMs, Easing.CubicOut),
            Sheet.FadeToAsync(1, FadeMs),
            Scrim.FadeToAsync(1, FadeMs));
    }

    private void OnCancelTapped(object? sender, EventArgs e)
    {
        if (_viewModel is not null && _viewModel.CancelCommand.CanExecute(null))
            _viewModel.CancelCommand.Execute(null);
    }

    private async Task DismissAsync(bool saved)
    {
        if (!_isOpen)
            return;

        _isOpen = false;

        await Task.WhenAll(
            Sheet.TranslateToAsync(0, SlideDistance(), ExitMs, Easing.CubicIn),
            Sheet.FadeToAsync(0, FadeMs),
            Scrim.FadeToAsync(0, FadeMs));

        IsVisible = false;
        Sheet.TranslationY = 0;
        Sheet.Opacity = 1;

        if (saved)
            _service?.NotifySaved();
    }

    /// <summary>
    /// Bounds both the scrolling form and the sheet itself so it can never
    /// grow past the page. The sheet is anchored to the bottom, so anything
    /// taller than the page would push its top edge off-screen.
    /// </summary>
    private void ClampSheetHeight()
    {
        if (FormScroll is null || Sheet is null)
            return;

        var page = Height > 0 ? Height : FallbackPageHeight;
        var maxForm = Math.Max(240, page * MaxSheetFraction - SheetChrome);

        FormScroll.MaximumHeightRequest = maxForm;
        Sheet.MaximumHeightRequest = maxForm + SheetChrome;
    }

    /// <summary>
    /// Offset that hides the sheet below the fold. Uses the host height rather
    /// than the sheet height: the sheet is bottom-anchored, so translating by
    /// the page height always clears it even before the sheet has been measured.
    /// </summary>
    private double SlideDistance() => Height > 0 ? Height : FallbackPageHeight;
}
