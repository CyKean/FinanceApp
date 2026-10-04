namespace FinanceApp.Application.Services;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Notifications;
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
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly INotificationService _notificationService;
    private readonly CreateBudgetDtoValidator _createValidator;
    private readonly UpdateBudgetDtoValidator _updateValidator;

    public BudgetService(
        IUnitOfWork unitOfWork,
        IBudgetRepository budgetRepository,
        ICategoryRepository categoryRepository,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        INotificationService notificationService,
        CreateBudgetDtoValidator createValidator,
        UpdateBudgetDtoValidator updateValidator,
        ILogger<BudgetService> logger) : base(unitOfWork, logger)
    {
        _budgetRepository = budgetRepository;
        _categoryRepository = categoryRepository;
        _accountRepository = accountRepository;
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

        var existingBudget = await _budgetRepository.GetActiveForCategoryAsync(userId, dto.CategoryId, dto.StartDate, cancellationToken);
        if (existingBudget != null)
            throw new DomainExceptions.ValidationException("An active budget already exists for this category in the selected period", "BUDGET_EXISTS");

        if (dto.LinkedAccountId.HasValue)
        {
            var linkedAccount = await _accountRepository.GetByIdAsync(dto.LinkedAccountId.Value.Value, cancellationToken);
            if (linkedAccount == null || linkedAccount.UserId != userId)
                throw new DomainExceptions.NotFoundException("Account", dto.LinkedAccountId.Value.Value);
        }

            var budget = new Budget(
                dto.Name,
                dto.Amount,
                dto.StartDate,
                dto.EndDate,
                dto.CategoryId,
                userId,
                dto.Icon,
                dto.Color,
                dto.LinkedAccountId);

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

        if (dto.LinkedAccountId.HasValue)
        {
            var linkedAccount = await _accountRepository.GetByIdAsync(dto.LinkedAccountId.Value.Value, cancellationToken);
            if (linkedAccount == null || linkedAccount.UserId != userId)
                throw new DomainExceptions.NotFoundException("Account", dto.LinkedAccountId.Value.Value);

            budget.UpdateLinkedAccount(dto.LinkedAccountId.Value);
        }

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
        var categories = await _categoryRepository.GetByIdsAsync(
            budgets.Select(b => b.CategoryId.Value).Distinct().ToList(), cancellationToken);

        return budgets.Select(b =>
        {
            categories.TryGetValue(b.CategoryId.Value, out var cat);

            return b.ToDto(cat?.Name ?? "", cat?.Icon ?? "", cat?.Color ?? "");
        }).ToList();
    }

    public async Task<IReadOnlyList<BudgetDto>> GetActiveAsync(Guid userId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var budgets = await _budgetRepository.GetActiveByUserIdAsync(userId, asOfDate, cancellationToken);
        var categories = await _categoryRepository.GetByIdsAsync(
            budgets.Select(b => b.CategoryId.Value).Distinct().ToList(), cancellationToken);

        // Only persist when a spent amount actually moved. Saving unconditionally
        // made every read take SQLite's write lock, so a page as innocent as
        // Forecasts or Budget Ideas blocked the UI thread of whatever page loaded
        // next - the UI thread then sat waiting on the lock and Android raised
        // "Finora isn't responding".
        var changed = false;

        foreach (var budget in budgets)
        {
            changed |= await CalculateAndSetSpentAmount(budget, cancellationToken);
        }

        if (changed)
        {
            await UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        return budgets.Select(b =>
        {
            categories.TryGetValue(b.CategoryId.Value, out var cat);

            return b.ToDto(cat?.Name ?? "", cat?.Icon ?? "", cat?.Color ?? "");
        }).ToList();
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

        // Alerts only fire on writes - reads would re-notify on every page load.
        await NotifyIfOverThresholdAsync(budget, cancellationToken);
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

    public async Task RecalculateSpentAsync(Guid budgetId, Guid userId, CancellationToken cancellationToken = default)
    {
        var budget = await _budgetRepository.GetByIdAsync(budgetId, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("Budget", budgetId);

        if (budget.UserId != userId)
            throw new DomainExceptions.NotFoundException("Budget", budgetId);

        await CalculateAndSetSpentAmount(budget, cancellationToken);
        budget.MarkAsPendingUpdate();
        await _budgetRepository.UpdateAsync(budget, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Recomputes a budget's spent total from its transactions.
    /// </summary>
    /// <returns>True when the stored total moved, so the caller knows a save is
    /// needed. False means the read was pure and no write lock was taken.</returns>
    private async Task<bool> CalculateAndSetSpentAmount(Budget budget, CancellationToken cancellationToken)
    {
        var spent = await _transactionRepository.GetTotalByCategoryAsync(
            budget.UserId,
            budget.CategoryId,
            budget.StartDate,
            budget.EndDate,
            cancellationToken);

        var previous = budget.SpentAmount;

        budget.ResetSpending();
        budget.AddSpending(spent);

        return budget.SpentAmount != previous;
    }

    /// <summary>
    /// Raises an alert when a budget crosses the 90% mark, so the user hears
    /// about it the moment spending pushes them over rather than the next time
    /// they happen to open the notifications page.
    /// </summary>
    private async Task NotifyIfOverThresholdAsync(Budget budget, CancellationToken cancellationToken)
    {
        if (!budget.IsNearLimit(90))
            return;

        var limit = budget.Amount.Amount;
        var spent = budget.SpentAmount.Amount;
        var used = budget.GetPercentageUsed();
        var over = budget.IsOverBudget();

        await _notificationService.PublishAsync(
            new AppNotification(
                over ? $"budget-over-{budget.Id}" : $"budget-near-{budget.Id}",
                "Budgets",
                over ? $"{budget.Name} is over budget" : $"{budget.Name} is close to its limit",
                over
                    ? $"Spent {spent:N0} of {limit:N0} ({used:F0}%). Cut back or raise the limit."
                    : $"{used:F0}% used · {Math.Max(limit - spent, 0):N0} left of {limit:N0}.",
                over ? "alertCircle" : "pie",
                over ? NotificationSeverity.Critical : NotificationSeverity.Warning,
                DateTime.Now,
                "//Budgets"),
            cancellationToken);
    }
}