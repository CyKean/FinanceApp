namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

[QueryProperty(nameof(BudgetIdParam), "id")]
public partial class AddBudgetPage : ContentPage
{
    private readonly AddBudgetViewModel _viewModel;
    private Guid? _initializedFor;

    public string BudgetIdParam { get; set; } = string.Empty;

    public AddBudgetPage(AddBudgetViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        BuildEmojiGrid();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AddBudgetViewModel.Icon))
                BuildEmojiGrid();
        };
    }

    private void BuildEmojiGrid()
    {
        Views.Controls.FinoraIconPicker.Build(EmojiContainer, EmojiPalette.Icons, _viewModel.Icon, emoji =>
        {
            if (BindingContext is AddBudgetViewModel vm)
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
            // Always start collapsed even when the page instance is reused.
            _viewModel.IsAddingCategory = false;

            Guid? budgetId = Guid.TryParse(BudgetIdParam, out var parsedId) ? parsedId : null;

            if (_initializedFor == budgetId)
                return;

            _initializedFor = budgetId;
            await _viewModel.InitializeAsync(budgetId);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load the form: {ex.Message}", "OK");
            await Shell.Current.GoToAsync("//Budgets");
        }
    }
}
