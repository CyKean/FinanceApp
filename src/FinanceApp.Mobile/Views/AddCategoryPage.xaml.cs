namespace FinanceApp.Mobile.Views;

using FinanceApp.Domain.Enums;
using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

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
        BuildEmojiGrid();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AddCategoryViewModel.Icon))
                BuildEmojiGrid();
        };
    }

    private void BuildEmojiGrid()
    {
        Views.Controls.PaytinIconPicker.Build(EmojiContainer, EmojiPalette.Icons, _viewModel.Icon, emoji =>
        {
            if (BindingContext is AddCategoryViewModel vm)
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
            await DisplayAlertAsync("Error", $"Could not load the form: {ex.Message}", "OK");
            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception navEx)
            {
                System.Diagnostics.Debug.WriteLine($"Could not navigate back: {navEx.Message}");
                await Shell.Current.GoToAsync("//Main/More");
            }
        }
    }
}
