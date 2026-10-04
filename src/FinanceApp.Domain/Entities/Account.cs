namespace FinanceApp.Domain.Entities;

using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public class Account : Entity
{
    public string Name { get; private set; }
    public AccountType Type { get; private set; }

    /// <summary>
    /// The balance the account was opened with, in the account's own currency.
    /// This is the anchor a running balance is derived from, so it is the one
    /// balance input that is stored rather than recomputed.
    /// </summary>
    public decimal InitialBalanceAmount { get; private set; }

    /// <summary>
    /// The running balance shown to the user. It is always
    /// <see cref="InitialBalanceAmount"/> plus this account's transactions,
    /// recalculated by the balance service after every transaction change and
    /// after every sync, so it can never drift away from those transactions.
    /// </summary>
    public Money Balance { get; private set; }
    public string? Description { get; private set; }
    public string? Icon { get; private set; }
    public string? Color { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsDefault { get; private set; }
    public int SortOrder { get; private set; }

    private Account() : base() { }

    public Account(
        string name,
        AccountType type,
        Money initialBalance,
        Guid userId,
        string? description = null,
        string? icon = null,
        string? color = null,
        bool isDefault = false,
        int sortOrder = 0) : base()
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Account name cannot be empty", nameof(name));

        Name = name.Trim();
        Type = type;
        InitialBalanceAmount = initialBalance.Amount;
        Balance = initialBalance;
        UserId = userId;
        Description = description?.Trim();
        Icon = icon?.Trim();
        Color = color?.Trim();
        IsDefault = isDefault;
        SortOrder = sortOrder;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Account name cannot be empty", nameof(name));

        Name = name.Trim();
        UpdateTimestamp();
    }

    public void UpdateType(AccountType type)
    {
        Type = type;
        UpdateTimestamp();
    }

    public void UpdateDescription(string? description)
    {
        Description = description?.Trim();
        UpdateTimestamp();
    }

    public void UpdateIcon(string? icon)
    {
        Icon = icon?.Trim();
        UpdateTimestamp();
    }

    public void UpdateColor(string? color)
    {
        Color = color?.Trim();
        UpdateTimestamp();
    }

    public void SetAsDefault()
    {
        IsDefault = true;
        UpdateTimestamp();
    }

    public void UnsetAsDefault()
    {
        IsDefault = false;
        UpdateTimestamp();
    }

    public void UpdateSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
        UpdateTimestamp();
    }

    public void AdjustBalance(Money amount)
    {
        Balance = Balance.Add(amount);
        MarkAsPendingUpdate();
    }

    /// <summary>
    /// Replaces the running balance with a value derived from the account's
    /// transactions. Marking the row pending is what queues the correction for
    /// the outbox: without it the balance changed here and nowhere else, so it
    /// never reached Supabase and the next pull restored the server's older
    /// figure.
    /// </summary>
    public void SetBalance(Money balance)
    {
        // A recalculation that confirms the current figure is not a change, and
        // must not queue a push that would only be undone by the next pull.
        if (Balance.Equals(balance))
            return;

        Balance = balance;
        MarkAsPendingUpdate();
    }
}