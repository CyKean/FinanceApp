using FinanceApp.Application.Interfaces;
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

        _authService.AuthStateChanged += e => OnAuthStateChanged(e);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    private async void OnAuthStateChanged(FinanceApp.Application.Interfaces.AuthStateChangedEventArgs e)
    {
        if (MainPage is AppShell shell)
        {
            if (e.IsAuthenticated)
            {
                await shell.GoToAsync("//Main/Dashboard");
            }
            else
            {
                await shell.GoToAsync("//Login");
            }
        }
    }
}