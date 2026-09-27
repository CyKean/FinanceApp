namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class GoalsPage : ContentPage
{
    public GoalsPage(GoalsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is GoalsViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//More");
    }
}
