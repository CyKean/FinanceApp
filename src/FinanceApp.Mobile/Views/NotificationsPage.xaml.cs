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

        if (BindingContext is NotificationsViewModel vm)
        {
            vm.Attach();
            if (vm.LoadCommand.CanExecute(null))
                await vm.LoadCommand.ExecuteAsync(null);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // The centre is a singleton; drop the handler when this page goes away.
        (BindingContext as NotificationsViewModel)?.Detach();
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync(".."); }
        catch { await Shell.Current.GoToAsync("//Main/Dashboard"); }
    }
}