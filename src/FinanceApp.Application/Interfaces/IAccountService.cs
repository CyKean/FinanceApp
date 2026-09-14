namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.DTOs;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;

public interface IAccountService
{
    Task<AccountDto> CreateAsync(CreateAccountDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<AccountDto> UpdateAsync(Guid id, UpdateAccountDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<AccountDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AccountDto?> GetDefaultAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountDto>> GetByTypeAsync(Guid userId, AccountType type, CancellationToken cancellationToken = default);
    Task<Money> GetTotalBalanceAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SetDefaultAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}

public interface ICategoryService
{
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CategoryDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryDto>> GetByTypeAsync(Guid userId, CategoryType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryDto>> GetActiveByTypeAsync(Guid userId, CategoryType type, CancellationToken cancellationToken = default);
    Task InitializeDefaultCategoriesAsync(Guid userId, CancellationToken cancellationToken = default);
}