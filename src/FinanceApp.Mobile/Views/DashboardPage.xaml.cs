namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;
using Microsoft.Extensions.DependencyInjection;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private TransactionSheetService? _sheetService;
    private AppUpdatePromptService? _updatePrompt;

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

        PromptForUpdate();
    }

    /// <summary>
    /// Asks whether a newer Finora has been published. The service throttles the
    /// check itself, so this is safe to call on every appearance.
    /// </summary>
    private void PromptForUpdate()
    {
        _updatePrompt ??= Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services
            .GetService<AppUpdatePromptService>();

        if (_updatePrompt is null)
            return;

        // Fire-and-forget: an update check must never delay or block the dashboard,
        // and the service handles its own failures. The delay lets the dashboard
        // paint before any modal appears.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1500);
                await _updatePrompt.PromptIfAvailableAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update prompt failed: {ex.Message}");
            }
        });
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
