namespace FinanceApp.Domain.Entities;

using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public class RecurringTransaction : Entity
{
    public string Name { get; private set; }
    public TransactionType Type { get; private set; }
    public Money Amount { get; private set; }
    public RecurringFrequency Frequency { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public AccountId AccountId { get; private set; }
    public CategoryId CategoryId { get; private set; }
    public Guid UserId { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? LastGeneratedAt { get; private set; }
    public DateTime? NextDueDate { get; private set; }
    public bool IsActive { get; private set; }

    private RecurringTransaction() : base() { }

    public RecurringTransaction(
        string name,
        TransactionType type,
        Money amount,
        RecurringFrequency frequency,
        DateTime startDate,
        AccountId accountId,
        CategoryId categoryId,
        Guid userId,
        string? notes = null,
        DateTime? endDate = null) : base()
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Recurring transaction name cannot be empty", nameof(name));

        if (amount.Amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        if (accountId == default)
            throw new ArgumentException("AccountId is required", nameof(accountId));

        if (categoryId == default)
            throw new ArgumentException("CategoryId is required", nameof(categoryId));

        Name = name.Trim();
        Type = type;
        Amount = amount;
        Frequency = frequency;
        StartDate = startDate.Date;
        EndDate = endDate?.Date;
        AccountId = accountId;
        CategoryId = categoryId;
        UserId = userId;
        Notes = notes?.Trim();
        IsActive = true;
        NextDueDate = CalculateNextDueDate(startDate.Date);
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));

        Name = name.Trim();
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateAmount(Money amount)
    {
        if (amount.Amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        Amount = amount;
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateFrequency(RecurringFrequency frequency)
    {
        Frequency = frequency;
        NextDueDate = CalculateNextDueDate(LastGeneratedAt ?? StartDate);
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateDates(DateTime startDate, DateTime? endDate)
    {
        if (endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException("End date must be after start date");

        StartDate = startDate.Date;
        EndDate = endDate?.Date;
        NextDueDate = CalculateNextDueDate(startDate.Date);
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

    public void UpdateNotes(string? notes)
    {
        Notes = notes?.Trim();
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void Activate()
    {
        IsActive = true;
        NextDueDate = CalculateNextDueDate(DateTime.UtcNow.Date);
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void RecordGeneration(DateTime generatedAt)
    {
        LastGeneratedAt = generatedAt;
        NextDueDate = CalculateNextDueDate(generatedAt);
        UpdateTimestamp();
    }

    public DateTime CalculateNextDueDate(DateTime fromDate)
    {
        return Frequency switch
        {
            RecurringFrequency.Daily => fromDate.AddDays(1),
            RecurringFrequency.Weekly => fromDate.AddDays(7),
            RecurringFrequency.Monthly => fromDate.AddMonths(1),
            RecurringFrequency.Yearly => fromDate.AddYears(1),
            _ => fromDate.AddMonths(1)
        };
    }

    public bool IsDue(DateTime asOfDate)
    {
        if (!IsActive)
            return false;

        if (EndDate.HasValue && asOfDate.Date > EndDate.Value.Date)
            return false;

        if (!NextDueDate.HasValue)
            return false;

        return asOfDate.Date >= NextDueDate.Value.Date;
    }

    public Transaction GenerateTransaction(DateTime date)
    {
        if (!IsDue(date))
            throw new InvalidOperationException("Recurring transaction is not due yet");

        return new Transaction(
            Type,
            Amount,
            date,
            AccountId,
            CategoryId,
            UserId,
            Notes,
            Id);
    }
}