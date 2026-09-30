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
    private readonly ChatHistoryStore _historyStore;
    private readonly IDialogService _dialogService;
    private readonly ILogger<ChatViewModel> _logger;

    private Guid? _userId;

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
        ChatHistoryStore historyStore,
        IDialogService dialogService,
        ILogger<ChatViewModel> logger)
    {
        _chatService = chatService;
        _authService = authService;
        _historyStore = historyStore;
        _dialogService = dialogService;
        _logger = logger;
        Title = "AI Assistant";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            _userId = userId;
            var history = await _historyStore.GetAllAsync(userId.Value);

            Messages.Clear();
            foreach (var message in history)
                Messages.Add(message);

            UpdateHasMessages();
            MessagesChanged?.Invoke(this, EventArgs.Empty);
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
        if (string.IsNullOrEmpty(content) || IsTyping) return;

        var userId = _userId ?? await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue)
        {
            await _dialogService.ShowFailureAsync("Please sign in first");
            return;
        }

        _userId = userId;
        Draft = string.Empty;

        var userMessage = new ChatMessageDto(Guid.NewGuid(), ChatRoles.User, content, DateTime.Now);
        Messages.Add(userMessage);
        UpdateHasMessages();
        MessagesChanged?.Invoke(this, EventArgs.Empty);
        FocusRequested?.Invoke(this, EventArgs.Empty);

        IsTyping = true;

        try
        {
            var reply = await _chatService.AskAsync(userId.Value, content, Messages.ToList());
            var assistantMessage = new ChatMessageDto(Guid.NewGuid(), ChatRoles.Assistant, reply.Content, DateTime.Now);
            Messages.Add(assistantMessage);

            await _historyStore.AppendAsync(userId.Value, userMessage);
            await _historyStore.AppendAsync(userId.Value, assistantMessage);

            MessagesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat reply");
            await _dialogService.ShowFailureAsync("Couldn't get a reply. Please try again.");
        }
        finally
        {
            IsTyping = false;
        }
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

    private void UpdateHasMessages()
    {
        HasMessages = Messages.Count > 0;
    }
}
