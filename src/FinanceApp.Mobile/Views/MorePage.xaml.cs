namespace FinanceApp.Mobile.Views;

public partial class MorePage : ContentPage
{
    public MorePage()
    {
        InitializeComponent();
    }

    private async void OnGoalsTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Goals");
    }

    private async void OnCategoriesTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Categories");
    }

    private async void OnAnalyticsTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Analytics");
    }

    private async void OnProfileTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Settings");
    }

    private async void OnScrimTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Main/Dashboard");
    }
}
