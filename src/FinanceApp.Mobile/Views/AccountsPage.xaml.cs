namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.ViewModels;

public partial class AccountsPage : ContentPage
{
    public AccountsPage(AccountsViewModel viewModel)
    {
        InitializeComponent();
        viewModel.AnimateDeleteAsync = DeletionAnimator.Create(this);
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AccountsViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnHomeTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Main/Dashboard");
    }
}
