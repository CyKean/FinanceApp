namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class DashboardPage : ContentPage
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _ = Helpers.PageAnimator.StaggerInAsync(ContentStack);
        if (BindingContext is DashboardViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
