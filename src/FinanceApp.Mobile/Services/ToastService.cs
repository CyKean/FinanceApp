namespace FinanceApp.Mobile.Services;

/// <summary>
/// Toast kind driving the icon + accent of the toast card.
/// </summary>
public enum ToastKind
{
    Success,
    Error,
    Info
}

public sealed record ToastRequest(string Message, ToastKind Kind);

/// <summary>
/// Global toast bus. View-models publish via IDialogService; any page hosting
/// a <see cref="Views.Controls.ToastHost"/> renders it. Latest toast wins.
/// </summary>
public sealed class ToastService
{
    public event Action<ToastRequest>? ToastRequested;

    public Task ShowAsync(string message, ToastKind kind = ToastKind.Success)
    {
        if (!string.IsNullOrWhiteSpace(message))
            ToastRequested?.Invoke(new ToastRequest(message, kind));
        return Task.CompletedTask;
    }
}
