namespace FinanceApp.Application.Services;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Validators;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using DomainExceptions = FinanceApp.Domain.Exceptions;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Application.Mappings;
using FluentValidation;
using Microsoft.Extensions.Logging;

public class BudgetService : BaseService, IBudgetService
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly INotificationService _notificationService;
    private readonly CreateBudgetDtoValidator _createValidator;
    private readonly UpdateBudgetDtoValidator _updateValidator;

    public BudgetService(
        IUnitOfWork unitOfWork,
        IBudgetRepository budgetRepository,
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        INotificationService notificationService,
        CreateBudgetDtoValidator createValidator,
        UpdateBudgetDtoValidator updateValidator,
        ILogger<BudgetService> logger) : base(unitOfWork, logger)
    {
        _budgetRepository = budgetRepository;
        _categoryRepository = categoryRepository;
        _transactionRepository = transactionRepository;
        _notificationService = notificationService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<BudgetDto> CreateAsync(CreateBudgetDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var category = await _categoryRepository.GetByIdAsync(dto.CategoryId.Value, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("Category", dto.CategoryId.Value);

        if (category.UserId != userId)
            throw new DomainExceptions.NotFoundException("Category", dto.CategoryId.Value);

        if (category.Type != CategoryType.Expense)
            throw new DomainExceptions.ValidationException("Budgets can only be created for expense categories", "INVALID_CATEGORY_TYPE");

        var existingBudget = await _budgetRepository.GetActiveForCategoryAsync(userId, dto.CategoryId, dto.StartDate, cancellationToken);
        if (existingBudget != null)
            throw new DomainExceptions.ValidationException("An active budget already exists for this category in the selected period", "BUDGET_EXISTS");

            var budget = new Budget(
                dto.Name,
                dto.Amount,
                dto.StartDate,
                dto.EndDate,
                dto.CategoryId,
                userId,
                dto.Icon,
                dto.Color);

        await CalculateAndSetSpentAmount(budget, cancellationToken);

        await _budgetRepository.AddAsync(budget, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Created budget {BudgetId} for user {UserId}", budget.Id, userId);
        return budget.ToDto(category.Name, category.Icon ?? "", category.Color ?? "");
    }

    public async Task<BudgetDto> UpdateAsync(Guid id, UpdateBudgetDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var budget = await _budgetRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("Budget", id);

        if (budget.UserId != userId)
            throw new DomainExceptions.NotFoundException("Budget", id);

        if (dto.Name != null)
            budget.UpdateName(dto.Name);

        if (dto.Amount != null)
            budget.UpdateAmount(dto.Amount);

        if (dto.Icon != null)
            budget.UpdateIcon(dto.Icon);

        if (dto.Color != null)
            budget.UpdateColor(dto.Color);

        if (dto.StartDate.HasValue || dto.EndDate.HasValue)
        {
            var startDate = dto.StartDate ?? budget.StartDate;
            var endDate = dto.EndDate ?? budget.EndDate;
            budget.UpdateDates(startDate, endDate);
        }

        if (dto.CategoryId.HasValue)
        {
            var newCategory = await _categoryRepository.GetByIdAsync(dto.CategoryId.Value.Value, cancellationToken)
                ?? throw new DomainExceptions.NotFoundException("Category", dto.CategoryId.Value.Value);

            if (newCategory.UserId != userId)
                throw new DomainExceptions.NotFoundException("Category", dto.CategoryId.Value.Value);

            if (newCategory.Type != CategoryType.Expense)
                throw new DomainExceptions.ValidationException("Budgets can only be created for expense categories", "INVALID_CATEGORY_TYPE");

            budget.UpdateCategory(dto.CategoryId.Value);
        }

        await CalculateAndSetSpentAmount(budget, cancellationToken);

        budget.MarkAsPendingUpdate();
        await _budgetRepository.UpdateAsync(budget, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        var cat = await _categoryRepository.GetByIdAsync(budget.CategoryId.Value, cancellationToken);
        Logger.LogInformation("Updated budget {BudgetId} for user {UserId}", budget.Id, userId);
        return budget.ToDto(cat?.Name ?? "", cat?.Icon ?? "", cat?.Color ?? "");
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var budget = await _budgetRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("Budget", id);

        if (budget.UserId != userId)
            throw new DomainExceptions.NotFoundException("Budget", id);

        budget.MarkAsDeleted();
        budget.MarkAsPendingDelete();
        await _budgetRepository.UpdateAsync(budget, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Deleted budget {BudgetId} for user {UserId}", budget.Id, userId);
    }

    public async Task<BudgetDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var budget = await _budgetRepository.GetByIdAsync(id, cancellationToken);
        if (budget == null || budget.UserId != userId)
            return null;

        var cat = await _categoryRepository.GetByIdAsync(budget.CategoryId.Value, cancellationToken);
        return budget.ToDto(cat?.Name ?? "", cat?.Icon ?? "", cat?.Color ?? "");
    }

    public async Task<IReadOnlyList<BudgetDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var budgets = await _budgetRepository.GetByUserIdAsync(userId, cancellationToken);
        var categoryIds = budgets.Select(b => b.CategoryId.Value).Distinct().ToList();

        var categories = new Dictionary<Guid, Category>();
        foreach (var catId in categoryIds)
        {
            var cat = await _categoryRepository.GetByIdAsync(catId, cancellationToken);
            if (cat != null) categories[catId] = cat;
        }

        return budgets.Select(b => b.ToDto(
            categories.TryGetValue(b.CategoryId.Value, out var cat) ? cat.Name : "",
            categories.TryGetValue(b.CategoryId.Value, out cat) ? cat.Icon ?? "" : "",
            categories.TryGetValue(b.CategoryId.Value, out cat) ? cat.Color ?? "" : ""
        )).ToList();
    }

    public async Task<IReadOnlyList<BudgetDto>> GetActiveAsync(Guid userId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var budgets = await _budgetRepository.GetActiveByUserIdAsync(userId, asOfDate, cancellationToken);
        var categoryIds = budgets.Select(b => b.CategoryId.Value).Distinct().ToList();

        var categories = new Dictionary<Guid, Category>();
        foreach (var catId in categoryIds)
        {
            var cat = await _categoryRepository.GetByIdAsync(catId, cancellationToken);
            if (cat != null) categories[catId] = cat;
        }

        foreach (var budget in budgets)
        {
            await CalculateAndSetSpentAmount(budget, cancellationToken);
            
            // Check for budget warnings
            if (budget.IsNearLimit(90))
            {
                await _notificationService.ScheduleBudgetWarningAsync(budget.Id, userId, cancellationToken);
            }
        }

        await UnitOfWork.SaveChangesAsync(cancellationToken);

        return budgets.Select(b => b.ToDto(
            categories.TryGetValue(b.CategoryId.Value, out var cat) ? cat.Name : "",
            categories.TryGetValue(b.CategoryId.Value, out cat) ? cat.Icon ?? "" : "",
            categories.TryGetValue(b.CategoryId.Value, out cat) ? cat.Color ?? "" : ""
        )).ToList();
    }

    public async Task<BudgetDto?> GetActiveForCategoryAsync(Guid userId, CategoryId categoryId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var budget = await _budgetRepository.GetActiveForCategoryAsync(userId, categoryId, asOfDate, cancellationToken);
        if (budget == null)
            return null;

        await CalculateAndSetSpentAmount(budget, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        var cat = await _categoryRepository.GetByIdAsync(budget.CategoryId.Value, cancellationToken);
        return budget.ToDto(cat?.Name ?? "", cat?.Icon ?? "", cat?.Color ?? "");
    }

    public async Task AddSpendingAsync(Guid budgetId, Money amount, Guid userId, CancellationToken cancellationToken = default)
    {
        var budget = await _budgetRepository.GetByIdAsync(budgetId, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("Budget", budgetId);

        if (budget.UserId != userId)
            throw new DomainExceptions.NotFoundException("Budget", budgetId);

        budget.AddSpending(amount);
        budget.MarkAsPendingUpdate();
        await _budgetRepository.UpdateAsync(budget, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        // Check for budget warnings after adding spending
        if (budget.IsNearLimit(90))
        {
            await _notificationService.ScheduleBudgetWarningAsync(budget.Id, userId, cancellationToken);
        }
    }

    public async Task RemoveSpendingAsync(Guid budgetId, Money amount, Guid userId, CancellationToken cancellationToken = default)
    {
        var budget = await _budgetRepository.GetByIdAsync(budgetId, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("Budget", budgetId);

        if (budget.UserId != userId)
            throw new DomainExceptions.NotFoundException("Budget", budgetId);

        budget.RemoveSpending(amount);
        budget.MarkAsPendingUpdate();
        await _budgetRepository.UpdateAsync(budget, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task CalculateAndSetSpentAmount(Budget budget, CancellationToken cancellationToken)
    {
        var spent = await _transactionRepository.GetTotalByCategoryAsync(
            budget.UserId,
            budget.CategoryId,
            budget.StartDate,
            budget.EndDate,
            cancellationToken);

        budget.ResetSpending();
        budget.AddSpending(spent);
    }
}