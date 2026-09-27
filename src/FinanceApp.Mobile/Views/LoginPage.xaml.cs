namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is LoginViewModel vm)
            await vm.CheckSavedSessionAsync();
    }
}