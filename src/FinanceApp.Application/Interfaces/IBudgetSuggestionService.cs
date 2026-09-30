namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.DTOs;

public interface IBudgetSuggestionService
{
    Task<IReadOnlyList<BudgetSuggestionDto>> GetSuggestionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
