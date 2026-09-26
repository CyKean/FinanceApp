namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}