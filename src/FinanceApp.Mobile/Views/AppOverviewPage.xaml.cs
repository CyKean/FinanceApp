namespace FinanceApp.Mobile.Views;

public partial class AppOverviewPage : ContentPage
{
    public AppOverviewPage()
    {
        InitializeComponent();
    }

    private static async void OnBackTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//More");
}
