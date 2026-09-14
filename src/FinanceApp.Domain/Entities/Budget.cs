namespace FinanceApp.Domain.Entities;

using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public class Budget : Entity
{
    public string Name { get; private set; }
    public Money Amount { get; private set; }
    public Money SpentAmount { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public CategoryId CategoryId { get; private set; }
    public Guid UserId { get; private set; }
    public SyncStatus SyncStatus { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }

    private Budget() : base() { }

    public Budget(
        string name,
        Money amount,
        DateTime startDate,
        DateTime endDate,
        CategoryId categoryId,
        Guid userId) : base()
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Budget name cannot be empty", nameof(name));

        if (amount.Amount <= 0)
            throw new ArgumentException("Budget amount must be positive", nameof(amount));

        if (endDate < startDate)
            throw new ArgumentException("End date must be after start date", nameof(endDate));

        if (categoryId == default)
            throw new ArgumentException("CategoryId is required", nameof(categoryId));

        Name = name.Trim();
        Amount = amount;
        SpentAmount = Money.Zero(amount.Currency);
        StartDate = startDate.Date;
        EndDate = endDate.Date;
        CategoryId = categoryId;
        UserId = userId;
        SyncStatus = SyncStatus.PendingCreate;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Budget name cannot be empty", nameof(name));

        Name = name.Trim();
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateAmount(Money amount)
    {
        if (amount.Amount <= 0)
            throw new ArgumentException("Budget amount must be positive", nameof(amount));

        Amount = amount;
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateDates(DateTime startDate, DateTime endDate)
    {
        if (endDate < startDate)
            throw new ArgumentException("End date must be after start date", nameof(endDate));

        StartDate = startDate.Date;
        EndDate = endDate.Date;
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

    public void AddSpending(Money amount)
    {
        if (amount.Currency != Amount.Currency)
            throw new InvalidOperationException("Currency mismatch");

        SpentAmount = SpentAmount.Add(amount);
        UpdateTimestamp();
    }

    public void RemoveSpending(Money amount)
    {
        if (amount.Currency != Amount.Currency)
            throw new InvalidOperationException("Currency mismatch");

        SpentAmount = SpentAmount.Subtract(amount);
        if (SpentAmount.Amount < 0)
            SpentAmount = Money.Zero(Amount.Currency);

        UpdateTimestamp();
    }

    public void ResetSpending()
    {
        SpentAmount = Money.Zero(Amount.Currency);
        UpdateTimestamp();
    }

    public Money GetRemainingAmount()
    {
        return Amount.Subtract(SpentAmount);
    }

    public decimal GetPercentageUsed()
    {
        if (Amount.Amount == 0)
            return 0;

        return Math.Round((SpentAmount.Amount / Amount.Amount) * 100, 2);
    }

    public bool IsOverBudget()
    {
        return SpentAmount > Amount;
    }

    public bool IsNearLimit(decimal thresholdPercent = 80)
    {
        return GetPercentageUsed() >= thresholdPercent;
    }

    public bool IsDateInRange(DateTime date)
    {
        var dateOnly = date.Date;
        return dateOnly >= StartDate && dateOnly <= EndDate;
    }

    public void MarkAsSynced()
    {
        SyncStatus = SyncStatus.Synced;
        LastSyncedAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void MarkAsPendingCreate()
    {
        SyncStatus = SyncStatus.PendingCreate;
        UpdateTimestamp();
    }

    public void MarkAsPendingUpdate()
    {
        if (SyncStatus == SyncStatus.Synced || SyncStatus == SyncStatus.PendingCreate)
            SyncStatus = SyncStatus.PendingUpdate;
        UpdateTimestamp();
    }

    public void MarkAsPendingDelete()
    {
        SyncStatus = SyncStatus.PendingDelete;
        UpdateTimestamp();
    }

    public void MarkAsFailed()
    {
        SyncStatus = SyncStatus.Failed;
        UpdateTimestamp();
    }
}