namespace FinanceApp.Application.DTOs;

using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public record CategoryDto(
    Guid Id,
    string Name,
    CategoryType Type,
    string? Icon,
    string? Color,
    Guid? ParentCategoryId,
    bool IsSystem,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDeleted
);

public record CreateCategoryDto(
    string Name,
    CategoryType Type,
    string? Icon = null,
    string? Color = null,
    Guid? ParentCategoryId = null
);

public record UpdateCategoryDto(
    string? Name = null,
    string? Icon = null,
    string? Color = null,
    int? SortOrder = null,
    bool? IsActive = null,
    Guid? ParentCategoryId = null
);