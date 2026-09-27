namespace FinanceApp.Mobile.Views;

using FinanceApp.Domain.Enums;
using FinanceApp.Mobile.ViewModels;

[QueryProperty(nameof(TransactionTypeParam), "type")]
public partial class AddTransactionSheetPage : ContentPage
{
    private readonly AddTransactionViewModel _viewModel;
    private TransactionType? _initializedType;

    public string TransactionTypeParam { get; set; } = nameof(TransactionType.Expense);

    public AddTransactionSheetPage(AddTransactionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.OnSavedCallback = () => Shell.Current.GoToAsync("//Dashboard");
        _viewModel.OnCancelledCallback = () => Shell.Current.GoToAsync("//Dashboard");
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await EnsureInitializedAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Re-applies the mode for reused page instances where the query
        // may arrive without a fresh navigation event.
        await EnsureInitializedAsync();
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
            System.Diagnostics.Debug.WriteLine($"[SHEET] Initializing {type}");
            await _viewModel.InitializeAsync(type);
            System.Diagnostics.Debug.WriteLine("[SHEET] Initialized OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SHEET-FAIL] {ex}");
            await DisplayAlert("Error", $"Could not load the form: {ex.Message}", "OK");
            await Shell.Current.GoToAsync("//Dashboard");
        }
    }
}
