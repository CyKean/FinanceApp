namespace FinanceApp.Application.Interfaces;

/// <summary>
/// Recomputes account balances from the accounts' own transactions.
/// </summary>
public interface IAccountBalanceService
{
    /// <summary>
    /// Recomputes one account's balance as its initial balance plus its income
    /// minus its expenses.
    /// </summary>
    Task<Domain.ValueObjects.Money> RecalculateAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes every account a user owns in one pass, and is what keeps the
    /// balances correct after a sync has pulled other devices' transactions in.
    /// </summary>
    Task<int> RecalculateAllAsync(Guid userId, CancellationToken cancellationToken = default);
}