namespace FinanceApp.Mobile.Services;

public sealed class SuccessAnimationRequest
{
    public SuccessAnimationRequest(string message)
    {
        Message = message;
    }

    public string Message { get; }

    public TaskCompletionSource<bool> Completion { get; } = new();
}

public sealed class SuccessAnimationService
{
    public event Action<SuccessAnimationRequest>? SuccessRequested;

    public bool HasHost => SuccessRequested != null;

    public Task ShowAsync(string message)
    {
        var request = new SuccessAnimationRequest(message);
        var handler = SuccessRequested;
        if (handler == null)
        {
            request.Completion.TrySetResult(true);
            return request.Completion.Task;
        }

        handler.Invoke(request);
        return request.Completion.Task;
    }
}
