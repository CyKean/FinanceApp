namespace FinanceApp.Domain.Entities;

using FinanceApp.Domain.Common;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public class Category : Entity
{
    public string Name { get; private set; }
    public CategoryType Type { get; private set; }
    public string? Icon { get; private set; }
    public string? Color { get; private set; }
    public Guid? ParentCategoryId { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsSystem { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private Category() : base() { }

    public Category(
        string name,
        CategoryType type,
        Guid userId,
        string? icon = null,
        string? color = null,
        Guid? parentCategoryId = null,
        bool isSystem = false,
        int sortOrder = 0) : base()
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name cannot be empty", nameof(name));

        Name = name.Trim();
        Type = type;
        UserId = userId;
        Icon = icon?.Trim();
        Color = color?.Trim();
        ParentCategoryId = parentCategoryId;
        IsSystem = isSystem;
        SortOrder = sortOrder;
        IsActive = true;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name cannot be empty", nameof(name));

        Name = name.Trim();
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

    public void UpdateSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
        UpdateTimestamp();
    }

    public void Activate()
    {
        IsActive = true;
        UpdateTimestamp();
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdateTimestamp();
    }

    public void SetParentCategory(Guid? parentCategoryId)
    {
        ParentCategoryId = parentCategoryId;
        UpdateTimestamp();
    }
}