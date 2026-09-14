namespace FinanceApp.Mobile.Services;

using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;

public class NavigationService : INavigationService
{
    private readonly ILogger<NavigationService> _logger;
    private readonly IServiceProvider _services;

    public NavigationService(ILogger<NavigationService> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }

    public Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        _logger.LogInformation("Navigating to {Route}", route);
        var shell = _services.GetRequiredService<AppShell>();
        return shell.GoToAsync(route, parameters);
    }

    public Task GoBackAsync()
    {
        _logger.LogInformation("Going back");
        var shell = _services.GetRequiredService<AppShell>();
        return shell.GoToAsync("..");
    }

    public Task GoToRootAsync()
    {
        _logger.LogInformation("Going to root");
        var shell = _services.GetRequiredService<AppShell>();
        return shell.GoToAsync("//");
    }
}

public class DialogService : IDialogService
{
    private readonly ILogger<DialogService> _logger;

    public DialogService(ILogger<DialogService> logger)
    {
        _logger = logger;
    }

    private static Page? GetMainPage()
    {
        var app = Application.Current;
        if (app == null) return null;
        
        var window = app.Windows.FirstOrDefault();
        return window?.Page;
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        var page = GetMainPage();
        if (page == null) return false;
        return await page.DisplayAlert(title, message, confirmText, cancelText);
    }

    public async Task ShowAlertAsync(string title, string message, string cancelText = "OK")
    {
        var page = GetMainPage();
        if (page == null) return;
        await page.DisplayAlert(title, message, cancelText);
    }

    public async Task<string?> ShowPromptAsync(string title, string message, string placeholder = "", string confirmText = "OK", string cancelText = "Cancel")
    {
        var page = GetMainPage();
        if (page == null) return null;
        return await page.DisplayPromptAsync(title, message, confirmText, cancelText, placeholder: placeholder);
    }

    public async Task ShowToastAsync(string message, ToastDuration duration = ToastDuration.Short)
    {
        _logger.LogInformation("Toast: {Message}", message);
        await Task.CompletedTask;
    }
}