namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SettingsViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//More");
    }

    private async void OnAiAssistantTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("AiSettings");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not open AI settings: {ex.Message}");
        }
    }
}
