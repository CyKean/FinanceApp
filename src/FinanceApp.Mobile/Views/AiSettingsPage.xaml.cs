namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class AiSettingsPage : ContentPage
{
    public AiSettingsPage(AiSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AiSettingsViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not pop AI settings: {ex.Message}");
            await Shell.Current.GoToAsync("//Settings");
        }
    }
}
