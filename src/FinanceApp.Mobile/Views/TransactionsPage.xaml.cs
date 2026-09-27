namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class TransactionsPage : ContentPage
{
    public TransactionsPage(TransactionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is TransactionsViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
