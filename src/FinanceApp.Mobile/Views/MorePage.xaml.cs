namespace FinanceApp.Mobile.Views;

using FinanceApp.Application.Interfaces;
using FinanceApp.Mobile.Helpers;

public partial class MorePage : ContentPage
{
    private readonly IAuthenticationService _authService;

    public MorePage(IAuthenticationService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var email = await _authService.GetCurrentUserEmailAsync();
        ProfileName.Text = UserDisplay.NameFromEmail(email);
        AvatarInitial.Text = UserDisplay.InitialFromEmail(email);
    }

    private async void OnGoalsTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Goals");
    }

    private async void OnCategoriesTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Categories");
    }

    private async void OnAnalyticsTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Analytics");
    }

    private async void OnChatTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Chat");
    }

    private async void OnBudgetSuggestionsTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("BudgetSuggestions");
    }

    private async void OnCalendarTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Calendar");
    }

    private async void OnPredictionsTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Predictions");
    }

    private async void OnProfileTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Settings");
    }

    private async void OnScrimTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Main/Dashboard");
    }

    private async void OnHomeTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Main/Dashboard");
    }

    private async void OnTransactionsTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Main/Transactions");
    }
}
