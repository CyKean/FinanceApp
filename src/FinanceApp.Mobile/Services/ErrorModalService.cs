namespace FinanceApp.Mobile.Services;

public sealed class ErrorModalRequest
{
    public ErrorModalRequest(string title, string message)
    {
        Title = title;
        Message = message;
    }

    public string Title { get; }

    public string Message { get; }

    public TaskCompletionSource<bool> Completion { get; } = new();
}

/// <summary>
/// Red-alert equivalent of <see cref="ConfirmModalService"/>, for failures that
/// are not a yes/no decision: the user only needs to acknowledge them.
/// </summary>
public sealed class ErrorModalService
{
    public event Action<ErrorModalRequest>? ErrorRequested;

    public bool HasHost => ErrorRequested != null;

    public Task ShowAsync(string title, string message)
    {
        var request = new ErrorModalRequest(title, message);
        var handler = ErrorRequested;
        if (handler == null)
        {
            request.Completion.TrySetResult(false);
            return request.Completion.Task;
        }

        handler.Invoke(request);
        return request.Completion.Task;
    }
}