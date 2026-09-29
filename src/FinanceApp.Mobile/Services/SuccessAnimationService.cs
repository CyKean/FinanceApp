namespace FinanceApp.Mobile.Services;

public enum AnimationKind
{
    Success,
    Failure
}

public sealed class SuccessAnimationRequest
{
    public SuccessAnimationRequest(string message, AnimationKind kind = AnimationKind.Success)
    {
        Message = message;
        Kind = kind;
    }

    public string Message { get; }

    public AnimationKind Kind { get; }

    public TaskCompletionSource<bool> Completion { get; } = new();
}

public sealed class SuccessAnimationService
{
    public event Action<SuccessAnimationRequest>? SuccessRequested;

    public bool HasHost => SuccessRequested != null;

    public Task ShowAsync(string message) => ShowAsync(message, AnimationKind.Success);

    public Task ShowAsync(string message, AnimationKind kind)
    {
        var request = new SuccessAnimationRequest(message, kind);
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
