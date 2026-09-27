namespace FinanceApp.Domain.Entities;

using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public class FinancialGoal : Entity
{
    public string Name { get; private set; }
    public Money TargetAmount { get; private set; }
    public Money CurrentAmount { get; private set; }
    public DateTime TargetDate { get; private set; }
    public DateTime StartDate { get; private set; }
    public GoalStatus Status { get; private set; }
    public string? Description { get; private set; }
    public string? Icon { get; private set; }
    public string? Color { get; private set; }
    public Guid UserId { get; private set; }
    public AccountId? LinkedAccountId { get; private set; }
    public SyncStatus SyncStatus { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }

    private FinancialGoal() : base() { }

    public FinancialGoal(
        string name,
        Money targetAmount,
        DateTime targetDate,
        Guid userId,
        DateTime? startDate = null,
        string? description = null,
        string? icon = null,
        string? color = null,
        AccountId? linkedAccountId = null) : base()
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Goal name cannot be empty", nameof(name));

        if (targetAmount.Amount <= 0)
            throw new ArgumentException("Target amount must be positive", nameof(targetAmount));

        if (targetDate.Date < DateTime.UtcNow.Date)
            throw new ArgumentException("Target date must be in the future", nameof(targetDate));

        Name = name.Trim();
        TargetAmount = targetAmount;
        CurrentAmount = Money.Zero(targetAmount.Currency);
        TargetDate = targetDate.Date;
        StartDate = (startDate ?? DateTime.UtcNow).Date;
        Status = GoalStatus.Active;
        Description = description?.Trim();
        Icon = icon?.Trim();
        Color = color?.Trim();
        UserId = userId;
        LinkedAccountId = linkedAccountId;
        SyncStatus = SyncStatus.PendingCreate;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Goal name cannot be empty", nameof(name));

        Name = name.Trim();
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateTargetAmount(Money targetAmount)
    {
        if (targetAmount.Amount <= 0)
            throw new ArgumentException("Target amount must be positive", nameof(targetAmount));

        TargetAmount = targetAmount;
        if (CurrentAmount.Currency != targetAmount.Currency)
            CurrentAmount = Money.Zero(targetAmount.Currency);

        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateTargetDate(DateTime targetDate)
    {
        if (targetDate.Date < DateTime.UtcNow.Date)
            throw new ArgumentException("Target date must be in the future", nameof(targetDate));

        TargetDate = targetDate.Date;
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void UpdateDescription(string? description)
    {
        Description = description?.Trim();
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

    public void AddProgress(Money amount)
    {
        if (amount.Currency != TargetAmount.Currency)
            throw new InvalidOperationException("Currency mismatch");

        CurrentAmount = CurrentAmount.Add(amount);
        if (CurrentAmount >= TargetAmount)
        {
            CurrentAmount = TargetAmount;
            Complete();
        }
        UpdateTimestamp();
    }

    public void RemoveProgress(Money amount)
    {
        if (amount.Currency != TargetAmount.Currency)
            throw new InvalidOperationException("Currency mismatch");

        CurrentAmount = CurrentAmount.Subtract(amount);
        if (CurrentAmount.Amount < 0)
            CurrentAmount = Money.Zero(TargetAmount.Currency);

        if (Status == GoalStatus.Completed)
            Reactivate();

        UpdateTimestamp();
    }

    public void SetProgress(Money amount)
    {
        if (amount.Currency != TargetAmount.Currency)
            throw new InvalidOperationException("Currency mismatch");

        CurrentAmount = amount.Amount < 0 ? Money.Zero(TargetAmount.Currency) : amount;

        if (CurrentAmount >= TargetAmount)
            Complete();
        else if (Status == GoalStatus.Completed)
            Reactivate();

        UpdateTimestamp();
    }

    public void Complete()
    {
        Status = GoalStatus.Completed;
        CurrentAmount = TargetAmount;
        UpdateTimestamp();
        MarkAsPendingUpdate();
    }

    public void Pause()
    {
        if (Status == GoalStatus.Active)
        {
            Status = GoalStatus.Paused;
            UpdateTimestamp();
            MarkAsPendingUpdate();
        }
    }

    public void Reactivate()
    {
        if (Status == GoalStatus.Paused || Status == GoalStatus.Completed)
        {
            Status = GoalStatus.Active;
            UpdateTimestamp();
            MarkAsPendingUpdate();
        }
    }

    public void Cancel()
      {
          Status = GoalStatus.Cancelled;
          UpdateTimestamp();
          MarkAsPendingUpdate();
      }

    /// <summary>
    /// Sets status directly from a synced snapshot (pull-merge only).
    /// </summary>
    public void SetStatus(GoalStatus status)
    {
        Status = status;
    }

    public Money GetRemainingAmount()
    {
        return TargetAmount.Subtract(CurrentAmount);
    }

    public decimal GetProgressPercentage()
    {
        if (TargetAmount.Amount == 0)
            return 0;

        return Math.Round((CurrentAmount.Amount / TargetAmount.Amount) * 100, 2);
    }

    public int GetDaysRemaining()
    {
        var days = (TargetDate.Date - DateTime.UtcNow.Date).Days;
        return Math.Max(0, days);
    }

    public Money GetRequiredMonthlySavings()
    {
        var monthsRemaining = Math.Max(1, (decimal)GetDaysRemaining() / 30);
        var remaining = GetRemainingAmount();
        return remaining.Divide(monthsRemaining);
    }

    public bool IsOnTrack(Money monthlySavings)
    {
        var required = GetRequiredMonthlySavings();
        return monthlySavings >= required;
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