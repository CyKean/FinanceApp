namespace FinanceApp.Mobile.Views;

using FinanceApp.Domain.Enums;
using FinanceApp.Mobile.ViewModels;

[QueryProperty(nameof(CategoryTypeParam), "type")]
[QueryProperty(nameof(CategoryIdParam), "id")]
public partial class AddCategoryPage : ContentPage
{
    private readonly AddCategoryViewModel _viewModel;
    private (CategoryType Type, Guid? Id)? _initializedFor;

    public string CategoryTypeParam { get; set; } = nameof(CategoryType.Expense);
    public string CategoryIdParam { get; set; } = string.Empty;

    public AddCategoryPage(AddCategoryViewModel viewModel)
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
            if (!Enum.TryParse(CategoryTypeParam, ignoreCase: true, out CategoryType type))
                type = CategoryType.Expense;

            Guid? categoryId = Guid.TryParse(CategoryIdParam, out var parsedId) ? parsedId : null;

            if (_initializedFor == (type, categoryId))
                return;

            _initializedFor = (type, categoryId);
            await _viewModel.InitializeAsync(type, categoryId);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load the form: {ex.Message}", "OK");
            await Shell.Current.GoToAsync("//Categories");
        }
    }
}
