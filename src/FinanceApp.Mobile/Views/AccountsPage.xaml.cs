namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class AccountsPage : ContentPage
{
    public AccountsPage(AccountsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AccountsViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
