namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;
using Microsoft.Extensions.DependencyInjection;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private TransactionSheetService? _sheetService;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        QuickAIIcon.Data = FinoraIcons.GetGeometry("bulb");
        QuickStatsIcon.Data = FinoraIcons.GetGeometry("chart");
        QuickForecastIcon.Data = FinoraIcons.GetGeometry("forecast");
        QuickMoreIcon.Data = FinoraIcons.GetGeometry("sliders");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Paired with Detach below: the notification centre is a singleton, so a
        // subscription taken in the view model constructor outlives the page.
        _viewModel.Attach();

        // The add sheet is an overlay, so there is no navigation to trigger a
        // reload — refresh when it reports a saved transaction instead.
        if (_sheetService is null)
        {
            _sheetService = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services
                .GetService<TransactionSheetService>();
            if (_sheetService is not null)
                _sheetService.Saved += OnTransactionSaved;
        }

        _ = _viewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _sheetService?.Saved -= OnTransactionSaved;
        _sheetService = null;
        _viewModel.Detach();
    }

    private async void OnTransactionSaved()
    {
        if (_viewModel.LoadCommand.CanExecute(null))
            await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
