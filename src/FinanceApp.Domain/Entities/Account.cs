namespace FinanceApp.Domain.Entities;

using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public class Account : Entity
{
    public string Name { get; private set; }
    public AccountType Type { get; private set; }
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
        UpdateTimestamp();
    }

    public void SetBalance(Money balance)
    {
        Balance = balance;
        UpdateTimestamp();
    }
}