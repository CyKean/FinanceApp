namespace FinanceApp.Mobile.Services;

public sealed class ChoiceSheetRequest
{
    public ChoiceSheetRequest(string title, IReadOnlyList<string> options)
    {
        Title = title;
        Options = options;
    }

    public string Title { get; }

    public IReadOnlyList<string> Options { get; }

    public TaskCompletionSource<string?> Completion { get; } = new();
}

public sealed class ChoiceSheetService
{
    public event Action<ChoiceSheetRequest>? ChoiceRequested;

    public Task<string?> ShowAsync(string title, params string[] options)
    {
        var request = new ChoiceSheetRequest(title, options);
        var handler = ChoiceRequested;
        if (handler == null)
        {
            request.Completion.TrySetResult(null);
            return request.Completion.Task;
        }

        handler.Invoke(request);
        return request.Completion.Task;
    }
}
