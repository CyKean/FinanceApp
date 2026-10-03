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

        if (BindingContext is not BudgetSuggestionsViewModel vm || !vm.LoadCommand.CanExecute(null))
            return;

        // OnAppearing is async void: anything escaping here is rethrown on the
        // sync context and takes the whole process down.
        try
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Suggestion load failed: {ex}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Stop any in-flight analysis instead of letting it finish against a
        // page the user has already left.
        (BindingContext as BudgetSuggestionsViewModel)?.Cancel();
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        // Abandon the analysis before navigating. Cancellation is a signal to the
        // worker, not a wait, so this returns immediately and the UI thread stays
        // free for the page being revealed.
        (BindingContext as BudgetSuggestionsViewModel)?.Cancel();

        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not pop suggestions: {ex.Message}");

            try
            {
                await Shell.Current.GoToAsync("//Main/Budgets");
            }
            catch (Exception fallback)
            {
                System.Diagnostics.Debug.WriteLine($"Could not pop suggestions at all: {fallback.Message}");
            }
        }
    }
}
