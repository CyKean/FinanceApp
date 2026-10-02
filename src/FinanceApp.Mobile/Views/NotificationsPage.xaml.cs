namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class NotificationsPage : ContentPage
{
    public NotificationsPage(NotificationsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is NotificationsViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync(".."); }
        catch { await Shell.Current.GoToAsync("//Main/Dashboard"); }
    }
}