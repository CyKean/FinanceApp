namespace FinanceApp.Application.Services;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Validators;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using DomainExceptions = FinanceApp.Domain.Exceptions;
using FinanceApp.Application.Mappings;
using FluentValidation;
using Microsoft.Extensions.Logging;

public class CategoryService : BaseService, ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly CreateCategoryDtoValidator _createValidator;
    private readonly UpdateCategoryDtoValidator _updateValidator;

    private static readonly (string Name, string Icon, string Color, CategoryType Type)[] DefaultExpenseCategories = new[]
    {
        ("Food", "🍔", "#FF6B6B", CategoryType.Expense),
        ("Transportation", "🚌", "#4ECDC4", CategoryType.Expense),
        ("Housing", "🏠", "#45B7D1", CategoryType.Expense),
        ("Utilities", "⚡", "#FFBE0B", CategoryType.Expense),
        ("Shopping", "🛍️", "#FB5607", CategoryType.Expense),
        ("Entertainment", "🎮", "#8338EC", CategoryType.Expense),
        ("Health", "🏥", "#3A86FF", CategoryType.Expense),
        ("Education", "📚", "#06D6A0", CategoryType.Expense),
        ("Bills", "📄", "#118AB2", CategoryType.Expense),
        ("Subscriptions", "🔄", "#073B4C", CategoryType.Expense),
        ("Other", "📦", "#6C757D", CategoryType.Expense)
    };

    private static readonly (string Name, string Icon, string Color, CategoryType Type)[] DefaultIncomeCategories = new[]
    {
        ("Salary", "💼", "#28A745", CategoryType.Income),
        ("Freelance", "💻", "#20C997", CategoryType.Income),
        ("Business", "🏢", "#17A2B8", CategoryType.Income),
        ("Allowance", "💰", "#FFC107", CategoryType.Income),
        ("Investment", "📈", "#6F42C1", CategoryType.Income),
        ("Other", "📦", "#6C757D", CategoryType.Income)
    };

    public CategoryService(
        IUnitOfWork unitOfWork,
        ICategoryRepository categoryRepository,
        CreateCategoryDtoValidator createValidator,
        UpdateCategoryDtoValidator updateValidator,
        ILogger<CategoryService> logger) : base(unitOfWork, logger)
    {
        _categoryRepository = categoryRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var existing = await _categoryRepository.GetByNameAsync(userId, dto.Name, dto.Type, cancellationToken);
        if (existing != null)
            throw new DomainExceptions.ValidationException($"Category '{dto.Name}' already exists for this type", "DUPLICATE_CATEGORY");

        var maxSortOrder = (await _categoryRepository.GetByUserIdAsync(userId, cancellationToken))
            .Where(c => c.Type == dto.Type)
            .Max(c => (int?)c.SortOrder) ?? 0;

        var category = new Category(
            dto.Name,
            dto.Type,
            userId,
            dto.Icon,
            dto.Color,
            dto.ParentCategoryId,
            false,
            maxSortOrder + 1);

        await _categoryRepository.AddAsync(category, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Created category {CategoryId} for user {UserId}", category.Id, userId);
        return category.ToDto();
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("Category", id);

        if (category.UserId != userId)
            throw new DomainExceptions.NotFoundException("Category", id);

        if (category.IsSystem)
            throw new DomainExceptions.InvalidOperationDomainException("Cannot modify system categories");

        if (dto.Name != null)
        {
            var existing = await _categoryRepository.GetByNameAsync(userId, dto.Name, category.Type, cancellationToken);
            if (existing != null && existing.Id != id)
                throw new DomainExceptions.ValidationException($"Category '{dto.Name}' already exists for this type", "DUPLICATE_CATEGORY");

            category.UpdateName(dto.Name);
        }

        if (dto.Icon != null)
            category.UpdateIcon(dto.Icon);

        if (dto.Color != null)
            category.UpdateColor(dto.Color);

        if (dto.SortOrder.HasValue)
            category.UpdateSortOrder(dto.SortOrder.Value);

        if (dto.IsActive.HasValue)
        {
            if (dto.IsActive.Value)
                category.Activate();
            else
                category.Deactivate();
        }

        if (dto.ParentCategoryId.HasValue)
            category.SetParentCategory(dto.ParentCategoryId.Value);

        category.MarkAsPendingUpdate();
        await _categoryRepository.UpdateAsync(category, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Updated category {CategoryId} for user {UserId}", category.Id, userId);
        return category.ToDto();
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("Category", id);

        if (category.UserId != userId)
            throw new DomainExceptions.NotFoundException("Category", id);

        if (category.IsSystem)
            throw new DomainExceptions.InvalidOperationDomainException("Cannot delete system categories");

        category.MarkAsDeleted();
        category.MarkAsPendingDelete();
        await _categoryRepository.UpdateAsync(category, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Deleted category {CategoryId} for user {UserId}", category.Id, userId);
    }

    public async Task<CategoryDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category == null || category.UserId != userId)
            return null;

        return category.ToDto();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetByUserIdAsync(userId, cancellationToken);
        return categories.Select(c => c.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetByTypeAsync(Guid userId, CategoryType type, CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetByTypeAsync(userId, type, cancellationToken);
        return categories.Select(c => c.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetActiveByTypeAsync(Guid userId, CategoryType type, CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetActiveByTypeAsync(userId, type, cancellationToken);
        return categories.Select(c => c.ToDto()).ToList();
    }

    public async Task InitializeDefaultCategoriesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var existingCategories = (await _categoryRepository.GetByUserIdAsync(userId, cancellationToken)).ToList();

        foreach (var (name, icon, color, type) in DefaultExpenseCategories.Concat(DefaultIncomeCategories))
        {
            var exists = existingCategories.Any(c => c.Name == name && c.Type == type);
            if (!exists)
            {
                var maxSortOrder = existingCategories
                    .Where(c => c.Type == type)
                    .Max(c => (int?)c.SortOrder) ?? 0;

                var newCategory = new Category(name, type, userId, icon, color, null, true, maxSortOrder + 1);
                await _categoryRepository.AddAsync(newCategory, cancellationToken);
                existingCategories.Add(newCategory);
            }
        }

        await UnitOfWork.SaveChangesAsync(cancellationToken);
        Logger.LogInformation("Initialized default categories for user {UserId}", userId);
    }
}