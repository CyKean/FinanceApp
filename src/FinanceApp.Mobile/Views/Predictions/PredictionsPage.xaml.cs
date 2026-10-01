namespace FinanceApp.Mobile.Views.Predictions;

using FinanceApp.Mobile.ViewModels;

public partial class PredictionsPage : ContentPage
{
    public PredictionsPage(PredictionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is PredictionsViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync(".."); }
        catch { await Shell.Current.GoToAsync("//Main/More"); }
    }
}
