namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class BudgetsPage : ContentPage
{
    public BudgetsPage(BudgetsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is BudgetsViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
