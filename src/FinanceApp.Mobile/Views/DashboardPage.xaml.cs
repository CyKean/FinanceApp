namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.ViewModels;

public partial class DashboardPage : ContentPage
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        QuickAIIcon.Data = PaytinIcons.GetGeometry("bulb");
        QuickStatsIcon.Data = PaytinIcons.GetGeometry("chart");
        QuickForecastIcon.Data = PaytinIcons.GetGeometry("forecast");
        QuickMoreIcon.Data = PaytinIcons.GetGeometry("sliders");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DashboardViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
