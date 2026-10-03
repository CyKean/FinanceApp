namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

[QueryProperty(nameof(GoalIdParam), "id")]
public partial class AddGoalPage : ContentPage
{
    private readonly AddGoalViewModel _viewModel;
    private bool _hasInitialized;
    private Guid? _initializedFor;
    private string _goalIdParam = string.Empty;

    public string GoalIdParam
    {
        get => _goalIdParam;
        set
        {
            if (_goalIdParam == value) return;
            _goalIdParam = value;
            if (_viewModel is not null)
                _ = EnsureInitializedAsync();
        }
    }

    public AddGoalPage(AddGoalViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        BuildEmojiGrid();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AddGoalViewModel.Icon))
                BuildEmojiGrid();
        };
    }

    private void BuildEmojiGrid()
    {
        Views.Controls.FinoraIconPicker.Build(EmojiContainer, EmojiPalette.Icons, _viewModel.Icon, emoji =>
        {
            if (BindingContext is AddGoalViewModel vm)
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
            Guid? goalId = Guid.TryParse(_goalIdParam, out var parsedId) ? parsedId : null;

            if (_hasInitialized && _initializedFor == goalId)
                return;

            _hasInitialized = true;
            _initializedFor = goalId;
            await _viewModel.InitializeAsync(goalId);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load the form: {ex.Message}", "OK");
            await Shell.Current.GoToAsync("//Goals");
        }
    }
}
