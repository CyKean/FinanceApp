namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class AddTransactionPage : ContentPage
{
    public AddTransactionPage(AddTransactionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
