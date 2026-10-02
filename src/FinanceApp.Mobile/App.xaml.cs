using FinanceApp.Application.Interfaces;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace FinanceApp.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IAuthenticationService _authService;
    private readonly IServiceProvider _services;

    public App(IAuthenticationService authService, IServiceProvider services)
    {
        InitializeComponent();
        _authService = authService;
        _services = services;

        // Capture full .NET stacks for otherwise faceless JavaProxyThrowable crashes.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            System.Diagnostics.Debug.WriteLine($"[FATAL] Unhandled: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[FATAL] Unobserved task: {e.Exception}");
            e.SetObserved();
        };

        _authService.AuthStateChanged += e => OnAuthStateChanged(e);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<AppShell>());

        // Derive the notification feed up front so the bell badge is populated
        // before the user ever opens the notifications page, and keep it warm.
        var watcher = _services.GetRequiredService<NotificationWatcher>();
        watcher.Start();
        _ = watcher.RefreshAsync();

        return window;
    }

    private async void OnAuthStateChanged(FinanceApp.Application.Interfaces.AuthStateChangedEventArgs e)
    {
        if (Windows[0].Page is AppShell shell)
        {
            if (e.IsAuthenticated)
            {
                _ = _services.GetRequiredService<NotificationWatcher>().RefreshAsync();
                await shell.GoToAsync("//Main/Dashboard");
            }
            else
            {
                // Notifications belong to the user who was signed in; drop them
                // so the next account does not inherit their unread alerts.
                _services.GetRequiredService<NotificationWatcher>().Stop();
                _services.GetRequiredService<INotificationCenter>().Reset();

                await shell.GoToAsync("//Login");
            }
        }
    }
}