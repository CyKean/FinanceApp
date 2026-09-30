namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.DTOs;

public interface IFinanceChatService
{
    Task<ChatReplyDto> AskAsync(Guid userId, string question, IReadOnlyList<ChatMessageDto> history, CancellationToken cancellationToken = default);
}
