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
        BuildColorGrid();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AddBudgetViewModel.Color))
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
                    if (BindingContext is AddBudgetViewModel vm)
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
                    if (BindingContext is AddBudgetViewModel vm)
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
