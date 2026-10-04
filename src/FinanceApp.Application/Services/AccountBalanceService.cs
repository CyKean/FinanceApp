namespace FinanceApp.Application.Services;

using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

/// <summary>
/// Keeps every account balance equal to its initial balance plus the net of its
/// transactions.
/// <para>
/// Balance used to be a stored number that each transaction nudged by hand, and
/// the two halves of that arrangement drifted apart: a nudge that missed the
/// outbox was lost forever, and a pull that trusted the stored column put the
/// server's older figure back. Deriving the number instead means the balance
/// cannot disagree with the transactions the app is showing - there is only one
/// thing to get right, and it is recomputed after every change and every sync.
/// </para>
/// </summary>
public class AccountBalanceService : IAccountBalanceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<AccountBalanceService> _logger;

    public AccountBalanceService(
        IUnitOfWork unitOfWork,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        ILogger<AccountBalanceService> logger)
    {
        _unitOfWork = unitOfWork;
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<Money> RecalculateAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdIncludingDeletedAsync(accountId, cancellationToken);
        if (account == null)
            return Money.Zero();

        var net = await _transactionRepository.GetNetAmountsByAccountAsync(
            account.UserId, new AccountId(account.Id), cancellationToken);
        Apply(account, net);
        await _accountRepository.UpdateAsync(account, cancellationToken);
        await SaveAsync(cancellationToken);
        return account.Balance;
    }

    public async Task<int> RecalculateAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var accounts = await _accountRepository.GetByUserIdAsync(userId, cancellationToken);
        if (accounts.Count == 0)
            return 0;

        // One aggregate for every account, so recalculating after a sync costs a
        // single query however many accounts the user has.
        var net = await _transactionRepository.GetNetAmountsByAccountAsync(userId, accountId: null, cancellationToken);

        var corrected = 0;
        foreach (var account in accounts)
        {
            var before = account.Balance;
            Apply(account, net);
            if (!before.Equals(account.Balance))
                corrected++;

            await _accountRepository.UpdateAsync(account, cancellationToken);
        }

        if (corrected > 0)
        {
            _logger.LogInformation(
                "Recalculated {Corrected} of {Total} account balances for user {UserId} from their transactions",
                corrected, accounts.Count, userId);
        }

        await SaveAsync(cancellationToken);
        return accounts.Count;
    }

    /// <summary>
    /// Persists the recalculated balances. The repository's Update does not save,
    /// and this service is called from paths with different save timing - some
    /// inside a service transaction that commits later, some after the last save
    /// of the request - so it cannot rely on its caller to flush.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Balance is the opening balance plus the movements recorded in the
    /// account's own currency. Movements in another currency are skipped rather
    /// than added: Money refuses to add them, and silently converting would be
    /// worse than leaving them out of a balance we cannot do correctly.
    /// </summary>
    private static void Apply(Account account, IReadOnlyList<AccountNetAmount> netAmounts)
    {
        var currency = account.Balance.Currency;
        var balance = new Money(account.InitialBalanceAmount, currency);

        foreach (var net in netAmounts)
        {
            if (net.AccountId == account.Id && net.Currency == currency)
            {
                balance = balance.Add(new Money(net.Amount, currency));
                break;
            }
        }

        account.SetBalance(balance);
    }
}