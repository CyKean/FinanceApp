namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class ThemeSettingsPage : ContentPage
{
    public ThemeSettingsPage(ThemeSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ThemeSettingsViewModel vm)
            vm.RefreshSelection();
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not pop theme settings: {ex.Message}");
            await Shell.Current.GoToAsync("//Settings");
        }
    }
}
