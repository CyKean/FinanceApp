namespace FinanceApp.Mobile.Views;

using FinanceApp.Domain.Enums;
using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;

[QueryProperty(nameof(TransactionIdParam), "id")]
[QueryProperty(nameof(TransactionTypeParam), "type")]
public partial class AddTransactionPage : ContentPage
{
    private readonly AddTransactionViewModel _viewModel;
    private bool _hasInitialized;
    private Guid? _initializedFor;
    private string _transactionIdParam = string.Empty;

    public string TransactionIdParam
    {
        get => _transactionIdParam;
        set
        {
            if (_transactionIdParam == value) return;
            _transactionIdParam = value;
            if (_viewModel is not null)
                _ = EnsureInitializedAsync();
        }
    }

    public string TransactionTypeParam { get; set; } = nameof(TransactionType.Expense);

    public AddTransactionPage(AddTransactionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await EnsureInitializedAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _ = AddSheetReminder.WarnIfNeededAsync();
        await EnsureInitializedAsync();
    }

    private async Task EnsureInitializedAsync()
    {
        try
        {
            Guid? transactionId = Guid.TryParse(_transactionIdParam, out var parsedId) ? parsedId : null;

            if (!Enum.TryParse(TransactionTypeParam, ignoreCase: true, out TransactionType type))
                type = TransactionType.Expense;

            if (_hasInitialized && _initializedFor == transactionId)
                return;

            _hasInitialized = true;
            _initializedFor = transactionId;
            await _viewModel.InitializeAsync(type, transactionId);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load the form: {ex.Message}", "OK");
            await Shell.Current.GoToAsync("//Transactions");
        }
    }
}
