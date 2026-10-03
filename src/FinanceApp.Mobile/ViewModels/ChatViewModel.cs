namespace FinanceApp.Mobile.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Mobile.Services;
using Microsoft.Extensions.Logging;

public partial class ChatViewModel : BaseViewModel
{
    private readonly IFinanceChatService _chatService;
    private readonly IAuthenticationService _authService;
    private readonly IAiClient _aiClient;
    private readonly ChatHistoryStore _historyStore;
    private readonly IDialogService _dialogService;
    private readonly ILogger<ChatViewModel> _logger;

    private Guid? _userId;

    /// <summary>
    /// Set before the first await so two quick taps cannot both slip past the
    /// IsTyping check and fire two paid requests.
    /// </summary>
    private bool _sending;

    /// <summary>Cancelled when the conversation is cleared mid-reply.</summary>
    private CancellationTokenSource? _pending;

    /// <summary>False when no API key is stored, so the header can say so.</summary>
    [ObservableProperty]
    private bool _isConfigured;

    /// <summary>Header subtitle that reflects whether cloud AI is available.</summary>
    public string StatusLine => IsConfigured
        ? "Your money, and money basics"
        : "On-device answers only - add an API key in Settings";

    public ObservableCollection<ChatMessageDto> Messages { get; } = new();

    public IReadOnlyList<string> QuickReplies { get; } = new[]
    {
        "How am I doing this month?",
        "Can I afford 1,500 for dinner?",
        "How much did I spend on food?",
        "How can I save more money?"
    };

    public event EventHandler? MessagesChanged;
    public event EventHandler? FocusRequested;

    [ObservableProperty]
    private string _draft = string.Empty;

    [ObservableProperty]
    private bool _isTyping;

    [ObservableProperty]
    private bool _hasMessages;

    public ChatViewModel(
        IFinanceChatService chatService,
        IAuthenticationService authService,
        IAiClient aiClient,
        ChatHistoryStore historyStore,
        IDialogService dialogService,
        ILogger<ChatViewModel> logger)
    {
        _chatService = chatService;
        _authService = authService;
        _aiClient = aiClient;
        _historyStore = historyStore;
        _dialogService = dialogService;
        _logger = logger;
        Title = "AI Assistant";
    }

    partial void OnIsConfiguredChanged(bool value) => OnPropertyChanged(nameof(StatusLine));

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            IsConfigured = await _aiClient.IsConfiguredAsync();

            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            _userId = userId;
            var history = await _historyStore.GetAllAsync(userId.Value);

            RunOnUiThread(() =>
            {
                Messages.Clear();
                foreach (var message in history)
                    Messages.Add(message);

                UpdateHasMessages();
                MessagesChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading chat history");
            SetError("Failed to load conversation");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SendAsync(string? text)
    {
        var content = text?.Trim();
        if (string.IsNullOrEmpty(content) || _sending) return;

        // Claim the slot synchronously, before any await.
        _sending = true;
        IsTyping = true;

        try
        {
            var userId = _userId ?? await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                await _dialogService.ShowFailureAsync("Please sign in first");
                return;
            }

            _userId = userId;
            Draft = string.Empty;

            var userMessage = new ChatMessageDto(Guid.NewGuid(), ChatRoles.User, content, DateTime.Now);
            AddMessage(userMessage);
            FocusRequested?.Invoke(this, EventArgs.Empty);

            // Persist the question straight away. It used to be written only
            // after the reply came back, so a failure lost what the user typed.
            await _historyStore.AppendAsync(userId.Value, userMessage);

            var pending = new CancellationTokenSource();
            _pending = pending;

            var reply = await _chatService.AskAsync(userId.Value, content, Messages.ToList(), pending.Token);

            // Carry the source through so the bubble can show whether the answer
            // was computed on-device or sent to the AI provider.
            var assistantMessage = new ChatMessageDto(
                Guid.NewGuid(),
                ChatRoles.Assistant,
                reply.Content,
                DateTime.Now,
                reply.Source.ToString());
            AddMessage(assistantMessage);

            await _historyStore.AppendAsync(userId.Value, assistantMessage);
        }
        catch (OperationCanceledException)
        {
            // The conversation was cleared while the reply was in flight.
            _logger.LogInformation("Chat reply cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat reply");
            await _dialogService.ShowFailureAsync("Couldn't get a reply. Please try again.");
        }
        finally
        {
            _pending?.Dispose();
            _pending = null;
            _sending = false;
            IsTyping = false;
        }
    }

    private void AddMessage(ChatMessageDto message)
    {
        RunOnUiThread(() =>
        {
            Messages.Add(message);
            UpdateHasMessages();
            MessagesChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private static void RunOnUiThread(Action action)
    {
        if (MainThread.IsMainThread)
            action();
        else
            MainThread.BeginInvokeOnMainThread(action);
    }

    [RelayCommand]
    private async Task SendDraftAsync()
    {
        await SendAsync(Draft);
    }

    [RelayCommand]
    private async Task SendQuickReplyAsync(string reply)
    {
        await SendAsync(reply);
    }

    [RelayCommand]
    private async Task ClearChatAsync()
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Clear conversation",
            "Delete all messages in this conversation?",
            "Clear",
            "Cancel",
            destructive: true);

        if (!confirmed) return;

        // Stop the in-flight reply so it cannot repopulate a cleared chat.
        CancelPending();

        try
        {
            if (_userId.HasValue)
                await _historyStore.ClearAsync(_userId.Value);

            Messages.Clear();
            UpdateHasMessages();
            MessagesChanged?.Invoke(this, EventArgs.Empty);
            await _dialogService.ShowToastAsync("Conversation cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing chat");
            await _dialogService.ShowFailureAsync("Couldn't clear the conversation");
        }
    }

    private void CancelPending()
    {
        var pending = _pending;
        _pending = null;

        try
        {
            pending?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already finished.
        }
    }

    private void UpdateHasMessages()
    {
        HasMessages = Messages.Count > 0;
    }
}
