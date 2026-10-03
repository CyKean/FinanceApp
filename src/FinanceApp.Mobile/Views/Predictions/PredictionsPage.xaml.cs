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

        if (BindingContext is not PredictionsViewModel vm || !vm.LoadCommand.CanExecute(null))
            return;

        // OnAppearing is async void: anything escaping here is rethrown on the
        // sync context and takes the whole process down, which is how the app
        // used to vanish the moment this page opened.
        try
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Forecast load failed: {ex}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // The forecast pipeline is heavy; abandon it rather than burning CPU on
        // a page the user has already navigated away from.
        (BindingContext as PredictionsViewModel)?.Cancel();
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        try
        {
            (BindingContext as PredictionsViewModel)?.Cancel();
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not pop forecasts: {ex.Message}");

            try
            {
                await Shell.Current.GoToAsync("//Main/Dashboard");
            }
            catch (Exception fallback)
            {
                System.Diagnostics.Debug.WriteLine($"Could not pop forecasts at all: {fallback.Message}");
            }
        }
    }
}
