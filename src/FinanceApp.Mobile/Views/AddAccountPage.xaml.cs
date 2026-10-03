namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

[QueryProperty(nameof(AccountIdParam), "id")]
public partial class AddAccountPage : ContentPage
{
    private readonly AddAccountViewModel _viewModel;
    private bool _hasInitialized;
    private Guid? _initializedFor;
    private string _accountIdParam = string.Empty;

    public string AccountIdParam
    {
        get => _accountIdParam;
        set
        {
            if (_accountIdParam == value) return;
            _accountIdParam = value;
            if (_viewModel is not null)
                _ = EnsureInitializedAsync();
        }
    }

    public AddAccountPage(AddAccountViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        BuildEmojiGrid();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AddAccountViewModel.Icon))
                BuildEmojiGrid();
        };
    }

    private void BuildEmojiGrid()
    {
        Views.Controls.FinoraIconPicker.Build(EmojiContainer, EmojiPalette.Icons, _viewModel.Icon, emoji =>
        {
            if (BindingContext is AddAccountViewModel vm)
                vm.SelectIconCommand.Execute(emoji);
        });
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
            Guid? accountId = Guid.TryParse(_accountIdParam, out var parsedId) ? parsedId : null;

            if (_hasInitialized && _initializedFor == accountId)
                return;

            _hasInitialized = true;
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
