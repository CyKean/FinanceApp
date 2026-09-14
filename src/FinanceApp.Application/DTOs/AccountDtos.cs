namespace FinanceApp.Application.DTOs;

using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public record AccountDto(
    Guid Id,
    string Name,
    AccountType Type,
    Money Balance,
    string? Description,
    string? Icon,
    string? Color,
    bool IsDefault,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDeleted
);

public record CreateAccountDto(
    string Name,
    AccountType Type,
    Money InitialBalance,
    string? Description = null,
    string? Icon = null,
    string? Color = null,
    bool IsDefault = false
);

public record UpdateAccountDto(
    string? Name = null,
    AccountType? Type = null,
    string? Description = null,
    string? Icon = null,
    string? Color = null,
    bool? IsDefault = null,
    int? SortOrder = null
);