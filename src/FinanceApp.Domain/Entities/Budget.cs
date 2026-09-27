namespace FinanceApp.Domain.Entities;

using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public class Budget : Entity
{
    public string Name { get; private set; }
    public Money Amount { get; private set; }
    public Money SpentAmount { get; private set; } = Money.Zero("PHP");
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public CategoryId CategoryId { get; private set; }
    public Guid UserId { get; private set; }
    public AccountId? LinkedAccountId { get; private set; }
    public string? Icon { get; private set; }
    public string? Color { get; private set; }
    public SyncStatus SyncStatus { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }

    private Budget() : base() { }

    public Budget(
        string name,
        Money amount,
        DateTime startDate,
        DateTime endDate,
        CategoryId categoryId,
        Guid userId,
        string? icon = null,
        string? color = null,
        AccountId? linkedAccountId = null) : base()
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Budget name cannot be empty", nameof(name));

        if (amount == null)
            throw new ArgumentNullException(nameof(amount));

        if (string.IsNullOrWhiteSpace(amount.Currency))
            throw new ArgumentException("Currency cannot be empty", nameof(amount));

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
        Icon = icon?.Trim();
        Color = color?.Trim();
        LinkedAccountId = linkedAccountId;
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

    public void UpdateIcon(string? icon)
    {
        Icon = icon?.Trim();
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateColor(string? color)
    {
        Color = color?.Trim();
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateLinkedAccount(AccountId? accountId)
    {
        LinkedAccountId = accountId;
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    /// <summary>
    /// Sets the spent total from a synced snapshot (pull-merge only).
    /// </summary>
    public void SetSpentAmount(Money spentAmount)
    {
        SpentAmount = spentAmount;
    }

    public void UpdateAmount(Money amount)
    {
        if (amount == null)
            throw new ArgumentNullException(nameof(amount));

        if (amount.Amount <= 0)
            throw new ArgumentException("Budget amount must be positive", nameof(amount));

        Amount = amount;
        SpentAmount = Money.Zero(amount.Currency);
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
        if (Amount == null)
            throw new InvalidOperationException("Budget Amount is not initialized");

        if (amount == null)
            throw new ArgumentNullException(nameof(amount));

        if (string.IsNullOrWhiteSpace(amount.Currency))
            throw new ArgumentException("Amount currency cannot be empty", nameof(amount));

        if (string.IsNullOrWhiteSpace(Amount.Currency))
            throw new InvalidOperationException("Budget Amount currency is not initialized");

        if (amount.Currency != Amount.Currency)
            throw new InvalidOperationException("Currency mismatch");

        SpentAmount = SpentAmount.Add(amount);
        UpdateTimestamp();
    }

    public void RemoveSpending(Money amount)
    {
        if (Amount == null)
            throw new InvalidOperationException("Budget Amount is not initialized");

        if (amount == null)
            throw new ArgumentNullException(nameof(amount));

        if (string.IsNullOrWhiteSpace(amount.Currency))
            throw new ArgumentException("Amount currency cannot be empty", nameof(amount));

        if (string.IsNullOrWhiteSpace(Amount.Currency))
            throw new InvalidOperationException("Budget Amount currency is not initialized");

        if (amount.Currency != Amount.Currency)
            throw new InvalidOperationException("Currency mismatch");

        SpentAmount = SpentAmount.Subtract(amount);
        if (SpentAmount.Amount < 0)
            SpentAmount = Money.Zero(Amount.Currency);

        UpdateTimestamp();
    }

    public void ResetSpending()
    {
        if (Amount == null)
            throw new InvalidOperationException("Budget Amount is not initialized");

        SpentAmount = Money.Zero(Amount.Currency);
        UpdateTimestamp();
    }

    public Money GetRemainingAmount()
    {
        if (Amount == null)
            throw new InvalidOperationException("Budget Amount is not initialized");

        return Amount.Subtract(SpentAmount);
    }

    public decimal GetPercentageUsed()
    {
        if (Amount == null || Amount.Amount == 0)
            return 0;

        return Math.Round((SpentAmount.Amount / Amount.Amount) * 100, 2);
    }

    public bool IsOverBudget()
    {
        if (Amount == null)
            throw new InvalidOperationException("Budget Amount is not initialized");

        return SpentAmount > Amount;
    }

    public bool IsNearLimit(decimal thresholdPercent = 80)
    {
        if (Amount == null)
            throw new InvalidOperationException("Budget Amount is not initialized");

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

    public override bool Equals(object? obj)
    {
        if (obj is not Budget other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (Id == Guid.Empty || other.Id == Guid.Empty)
            return false;

        return Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(Budget? a, Budget? b)
    {
        if (a is null && b is null)
            return true;

        if (a is null || b is null)
            return false;

        return a.Equals(b);
    }

    public static bool operator !=(Budget? a, Budget? b)
    {
        return !(a == b);
    }
}