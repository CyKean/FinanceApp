namespace FinanceApp.Mobile.Services;

public sealed class ConfirmModalRequest
{
    public ConfirmModalRequest(string title, string message, string confirmText, string cancelText, bool destructive)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        IsDestructive = destructive;
    }

    public string Title { get; }

    public string Message { get; }

    public string ConfirmText { get; }

    public string CancelText { get; }

    public bool IsDestructive { get; }

    public TaskCompletionSource<bool> Completion { get; } = new();
}

public sealed class ConfirmModalService
{
    public event Action<ConfirmModalRequest>? ConfirmRequested;

    public bool HasHost => ConfirmRequested != null;

    public Task<bool> ShowAsync(
        string title,
        string message,
        string confirmText = "Yes",
        string cancelText = "No",
        bool destructive = false)
    {
        var request = new ConfirmModalRequest(title, message, confirmText, cancelText, destructive);
        var handler = ConfirmRequested;
        if (handler == null)
        {
            request.Completion.TrySetResult(false);
            return request.Completion.Task;
        }

        handler.Invoke(request);
        return request.Completion.Task;
    }
}
