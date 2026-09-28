namespace FinanceApp.Mobile.Services;

using FinanceApp.Application.Interfaces;

public interface INavigationService
{
    Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null);
    Task GoBackAsync();
    Task GoToRootAsync();
}

public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No");
    Task ShowAlertAsync(string title, string message, string cancelText = "OK");
    Task<string?> ShowPromptAsync(string title, string message, string placeholder = "", string confirmText = "OK", string cancelText = "Cancel");
    Task<string?> ShowActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons);
    Task<string?> ShowChoiceSheetAsync(string title, params string[] options);
    Task ShowToastAsync(string message, ToastDuration duration = ToastDuration.Short);
    Task ShowErrorToastAsync(string message);
}

public enum ToastDuration
{
    Short,
    Long
}