namespace FinanceApp.Mobile.Views;

using FinanceApp.Domain.Enums;
using FinanceApp.Mobile.ViewModels;

[QueryProperty(nameof(TransactionTypeParam), "type")]
public partial class AddTransactionSheetPage : ContentPage
{
    /// <summary>Resting offset for the sheet while it is off-screen.</summary>
    private const double OffscreenTranslation = 900;

    private const uint EnterMs = 300;
    private const uint EnterFadeMs = 220;
    private const uint ExitMs = 220;

    private readonly AddTransactionViewModel _viewModel;
    private TransactionType? _initializedType;
    private bool _isDismissing;

    public string TransactionTypeParam { get; set; } = nameof(TransactionType.Expense);

    public AddTransactionSheetPage(AddTransactionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // The sheet animates itself away before leaving, so navigation is
        // funnelled through DismissAsync instead of popping directly.
        _viewModel.OnSavedCallback = DismissAsync;
        _viewModel.OnCancelledCallback = DismissAsync;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await EnsureInitializedAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Shell reuses the page instance, so reset before animating in again.
        _isDismissing = false;

        // Re-applies the mode for reused page instances where the query
        // may arrive without a fresh navigation event.
        await EnsureInitializedAsync();
        await PlayEnterAsync();
    }

    private async Task PlayEnterAsync()
    {
        if (_isDismissing)
            return;

        // Reset first: the sheet may still hold the previous exit transform.
        Sheet.TranslationY = OffscreenTranslation;
        Scrim.Opacity = 0;

        await Task.WhenAll(
            Sheet.TranslateToAsync(0, 0, EnterMs, Easing.CubicOut),
            Scrim.FadeToAsync(1, EnterFadeMs));
    }

    private async Task DismissAsync()
    {
        if (_isDismissing)
            return;

        _isDismissing = true;

        await Task.WhenAll(
            Sheet.TranslateToAsync(0, OffscreenTranslation, ExitMs, Easing.CubicIn),
            Scrim.FadeToAsync(0, ExitMs));

        try
        {
            await Shell.Current.GoToAsync("//Dashboard");
        }
        catch
        {
            // Route unavailable — the sheet is already off-screen.
        }
    }

    private async Task EnsureInitializedAsync()
    {
        try
        {
            // Always start collapsed even when the page instance is reused.
            _viewModel.IsAddingAccount = false;
            _viewModel.IsAddingCategory = false;

            if (!Enum.TryParse(TransactionTypeParam, ignoreCase: true, out TransactionType type))
                type = TransactionType.Expense;

            if (_initializedType == type)
                return;

            _initializedType = type;
            await _viewModel.InitializeAsync(type);
        }
        catch (Exception)
        {
            await DisplayAlert("Error", "Could not load the form. Please try again.", "OK");
            await Shell.Current.GoToAsync("//Dashboard");
        }
    }
}
