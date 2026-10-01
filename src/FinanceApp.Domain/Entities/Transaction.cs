namespace FinanceApp.Domain.Entities;

using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public class Transaction : Entity
{
    public TransactionType Type { get; private set; }
    public Money Amount { get; private set; }
    public DateTime Date { get; private set; }
    public string? Notes { get; private set; }
    public AccountId AccountId { get; private set; }
    public CategoryId CategoryId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? RecurringTransactionId { get; private set; }

    private Transaction() : base() { }

    public Transaction(
        TransactionType type,
        Money amount,
        DateTime date,
        AccountId accountId,
        CategoryId categoryId,
        Guid userId,
        string? notes = null,
        Guid? recurringTransactionId = null) : base()
    {
        if (amount.Amount <= 0)
            throw new ArgumentException("Transaction amount must be positive", nameof(amount));

        if (accountId == default)
            throw new ArgumentException("AccountId is required", nameof(accountId));

        if (categoryId == default)
            throw new ArgumentException("CategoryId is required", nameof(categoryId));

        Type = type;
        Amount = amount;
        Date = date.Date;
        AccountId = accountId;
        CategoryId = categoryId;
        UserId = userId;
        Notes = notes?.Trim();
        RecurringTransactionId = recurringTransactionId;
    }

    public void UpdateAmount(Money amount)
    {
        if (amount.Amount <= 0)
            throw new ArgumentException("Transaction amount must be positive", nameof(amount));

        Amount = amount;
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateDate(DateTime date)
    {
        Date = date.Date;
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes?.Trim();
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateAccount(AccountId accountId)
    {
        if (accountId == default)
            throw new ArgumentException("AccountId is required", nameof(accountId));

        AccountId = accountId;
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateCategory(CategoryId categoryId)
    {
        if (categoryId == default)
            throw new ArgumentException("CategoryId is required", nameof(categoryId));

        CategoryId = categoryId;
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

}