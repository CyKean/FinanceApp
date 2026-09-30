namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class BudgetSuggestionsPage : ContentPage
{
    public BudgetSuggestionsPage(BudgetSuggestionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is BudgetSuggestionsViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not pop suggestions: {ex.Message}");
            await Shell.Current.GoToAsync("//Main/Budgets");
        }
    }
}
