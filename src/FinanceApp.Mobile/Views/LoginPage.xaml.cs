namespace FinanceApp.Mobile.Views;

using System.ComponentModel;
using FinanceApp.Mobile.ViewModels;
using Microsoft.Maui;

public partial class LoginPage : ContentPage
{
    private CancellationTokenSource? _restoreLoop;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        if (viewModel is INotifyPropertyChanged notify)
            notify.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is LoginViewModel vm)
        {
            if (vm.IsRestoringSession)
                StartRestoreLoop();
            await vm.CheckSavedSessionAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopRestoreLoop();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LoginViewModel.IsRestoringSession) || BindingContext is not LoginViewModel vm)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (vm.IsRestoringSession)
                StartRestoreLoop();
            else
                StopRestoreLoop();
        });
    }

    /// <summary>
    /// Breathes the restore hero while the session check runs. Without it a slow
    /// (or offline) check leaves the screen looking stuck on a static spinner.
    /// </summary>
    private void StartRestoreLoop()
    {
        StopRestoreLoop();

        if (RestoreFrame is null || RestoreHalo is null)
            return;

        var cts = new CancellationTokenSource();
        _restoreLoop = cts;
        _ = PulseAsync(cts.Token);
    }

    private async Task PulseAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await RestoreFrame.ScaleToAsync(1.05, 800, Easing.SinInOut);
                await RestoreHalo.FadeToAsync(0.12, 800, Easing.SinInOut);
                if (token.IsCancellationRequested) return;

                await Task.Delay(120, token);
                if (token.IsCancellationRequested) return;

                await RestoreFrame.ScaleToAsync(1.0, 800, Easing.SinInOut);
                await RestoreHalo.FadeToAsync(0.4, 800, Easing.SinInOut);
                await Task.Delay(120, token);
            }
        }
        catch (OperationCanceledException)
        {
            // The page went away or the session resolved; nothing to clean up.
        }
    }

    private void StopRestoreLoop()
    {
        _restoreLoop?.Cancel();
        _restoreLoop?.Dispose();
        _restoreLoop = null;

        // MAUI 10 animations do not take a token, so stop the in-flight ones.
        RestoreFrame?.AbortAnimation("ScaleTo");
        RestoreHalo?.AbortAnimation("FadeTo");

        if (RestoreFrame is not null)
        {
            RestoreFrame.Scale = 1;
            RestoreFrame.Opacity = 1;
        }

        if (RestoreHalo is not null)
            RestoreHalo.Opacity = 0.35;
    }
}
