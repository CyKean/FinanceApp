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
        BuildColorGrid();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AddCategoryViewModel.Color))
                BuildColorGrid();
        };
    }

    private void BuildEmojiGrid()
    {
        EmojiContainer.Children.Clear();
        foreach (var emoji in EmojiPalette.Icons)
        {
            var label = new Label
            {
                Text = emoji,
                FontSize = 24,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            var tile = new Border
            {
                WidthRequest = 48,
                HeightRequest = 48,
                StrokeThickness = 0,
                BackgroundColor = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
                Content = label,
                Margin = new Thickness(3)
            };
            var captured = emoji;
            tile.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    if (BindingContext is AddCategoryViewModel vm)
                        vm.SelectIconCommand.Execute(captured);
                })
            });
            EmojiContainer.Children.Add(tile);
        }
    }

    private void BuildColorGrid()
    {
        ColorContainer.Children.Clear();
        var selected = (_viewModel.Color ?? string.Empty).Trim();
        foreach (var hex in ColorPalette.Swatches)
        {
            var isSelected = string.Equals(hex, selected, StringComparison.OrdinalIgnoreCase);
            var tile = new Border
            {
                WidthRequest = 44,
                HeightRequest = 44,
                StrokeThickness = isSelected ? 3 : 0,
                Stroke = isSelected ? new SolidColorBrush(Color.FromArgb("#0E6B4F")) : Brush.Transparent,
                BackgroundColor = Color.FromArgb(hex),
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(22) },
                Margin = new Thickness(4)
            };
            var captured = hex;
            tile.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    if (BindingContext is AddCategoryViewModel vm)
                        vm.SelectColorCommand.Execute(captured);
                })
            });
            ColorContainer.Children.Add(tile);
        }
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
