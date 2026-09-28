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

    public async Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        _logger.LogInformation("Navigating to {Route}", route);
        var shell = _services.GetRequiredService<AppShell>();
        try
        {
            if (parameters == null)
                await shell.GoToAsync(route);
            else
                await shell.GoToAsync(route, parameters);
            _logger.LogInformation("Navigated to {Route}", route);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to {Route} failed", route);
            throw;
        }
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
    private readonly ToastService _toastService;
    private readonly ChoiceSheetService _choiceSheetService;

    public DialogService(ILogger<DialogService> logger, ToastService toastService, ChoiceSheetService choiceSheetService)
    {
        _logger = logger;
        _toastService = toastService;
        _choiceSheetService = choiceSheetService;
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
        return await page.DisplayAlertAsync(title, message, confirmText, cancelText);
    }

    public async Task ShowAlertAsync(string title, string message, string cancelText = "OK")
    {
        var page = GetMainPage();
        if (page == null) return;
        await page.DisplayAlertAsync(title, message, cancelText);
    }

    public async Task<string?> ShowActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons)
    {
        var page = GetMainPage();
        if (page == null) return null;
        return await page.DisplayActionSheet(title, cancel, destruction, buttons);
    }

    public Task<string?> ShowChoiceSheetAsync(string title, params string[] options)
    {
        _logger.LogInformation("Choice sheet: {Title}", title);
        return _choiceSheetService.ShowAsync(title, options);
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
        await _toastService.ShowAsync(message, ToastKind.Success);
    }

    public async Task ShowErrorToastAsync(string message)
    {
        _logger.LogWarning("Error toast: {Message}", message);
        await _toastService.ShowAsync(message, ToastKind.Error);
    }
}