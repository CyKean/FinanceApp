namespace FinanceApp.Application.DTOs;

using FinanceApp.Domain.ValueObjects;

public enum BudgetSuggestionKind
{
    Create,
    Increase,
    Decrease
}

public record BudgetSuggestionDto(
    string Id,
    BudgetSuggestionKind Kind,
    CategoryId CategoryId,
    string CategoryName,
    string CategoryIcon,
    string CategoryColor,
    Money SuggestedAmount,
    Money? CurrentAmount,
    Guid? TargetBudgetId,
    string Reason)
{
    public string KindLabel => Kind switch
    {
        BudgetSuggestionKind.Increase => "Raise budget",
        BudgetSuggestionKind.Decrease => "Lower budget",
        _ => "New budget"
    };

    public string KindIcon => Kind switch
    {
        BudgetSuggestionKind.Increase => "📈",
        BudgetSuggestionKind.Decrease => "📉",
        _ => "✨"
    };

    public string SuggestedText => SuggestedAmount.Amount.ToString("N0");

    public string IconGlyph => string.IsNullOrEmpty(CategoryIcon) ? "🎯" : CategoryIcon;

    public string CurrentText => CurrentAmount is null
        ? "No budget set for this category"
        : $"Current budget: {CurrentAmount.Amount.ToString("N0")}";
}

public static class ChatRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
}

public record ChatMessageDto(Guid Id, string Role, string Content, DateTime SentAt, string? Source = null)
{
    public bool IsUser => Role == ChatRoles.User;

    /// <summary>
    /// True when the answer came from the cloud provider rather than being
    /// computed on-device. Stored so the UI can be honest about it; null on
    /// messages written before this existed.
    /// </summary>
    public bool UsedCloud => string.Equals(Source, nameof(ChatReplySource.Assistant), StringComparison.Ordinal);

    public string TimeLabel => SentAt.ToString("HH:mm");
}

public enum ChatReplySource
{
    PersonalData,
    KnowledgeBase,
    Assistant,
    Fallback
}

public record ChatReplyDto(string Content, ChatReplySource Source);

public record AiTurn(string Role, string Content);

public record AiCompletionResult(bool Success, string? Content, string? Error, bool IsConfigured);
