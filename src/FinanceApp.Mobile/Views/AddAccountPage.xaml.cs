namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

[QueryProperty(nameof(AccountIdParam), "id")]
public partial class AddAccountPage : ContentPage
{
    private readonly AddAccountViewModel _viewModel;
    private Guid? _initializedFor;

    public string AccountIdParam { get; set; } = string.Empty;

    public AddAccountPage(AddAccountViewModel viewModel)
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
        await EnsureInitializedAsync();
    }

    private async Task EnsureInitializedAsync()
    {
        try
        {
            Guid? accountId = Guid.TryParse(AccountIdParam, out var parsedId) ? parsedId : null;

            if (_initializedFor == accountId)
                return;

            _initializedFor = accountId;
            await _viewModel.InitializeAsync(accountId);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load the form: {ex.Message}", "OK");
            await Shell.Current.GoToAsync("//Accounts");
        }
    }
}
