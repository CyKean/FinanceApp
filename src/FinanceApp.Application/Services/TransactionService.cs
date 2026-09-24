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

public class TransactionService : BaseService, ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBudgetService _budgetService;
    private readonly CreateTransactionDtoValidator _createValidator;
    private readonly UpdateTransactionDtoValidator _updateValidator;
    private readonly TransactionFilterDtoValidator _filterValidator;

    public TransactionService(
        IUnitOfWork unitOfWork,
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        IBudgetService budgetService,
        CreateTransactionDtoValidator createValidator,
        UpdateTransactionDtoValidator updateValidator,
        TransactionFilterDtoValidator filterValidator,
        ILogger<TransactionService> logger) : base(unitOfWork, logger)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
        _budgetService = budgetService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _filterValidator = filterValidator;
    }

    public async Task<TransactionDto> CreateAsync(CreateTransactionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        await ValidateTransactionAsync(dto, userId, cancellationToken);

        return await ExecuteInTransactionAsync(async () =>
        {
            var account = await _accountRepository.GetByIdAsync(dto.AccountId.Value, cancellationToken)
                ?? throw new DomainExceptions.NotFoundException("Account", dto.AccountId.Value);

            if (account.UserId != userId)
                throw new DomainExceptions.NotFoundException("Account", dto.AccountId.Value);

            var category = await _categoryRepository.GetByIdAsync(dto.CategoryId.Value, cancellationToken)
                ?? throw new DomainExceptions.NotFoundException("Category", dto.CategoryId.Value);

            if (category.UserId != userId)
                throw new DomainExceptions.NotFoundException("Category", dto.CategoryId.Value);

            if (category.Type != (dto.Type == TransactionType.Expense ? CategoryType.Expense : CategoryType.Income))
                throw new DomainExceptions.ValidationException("Category type does not match transaction type", "CATEGORY_TYPE_MISMATCH");

            var transaction = new Transaction(
                dto.Type,
                dto.Amount,
                dto.Date,
                dto.AccountId,
                dto.CategoryId,
                userId,
                dto.Notes,
                dto.RecurringTransactionId);

            await _transactionRepository.AddAsync(transaction, cancellationToken);

            var balanceChange = dto.Type == TransactionType.Income ? dto.Amount : new Money(-dto.Amount.Amount, dto.Amount.Currency);
            account.AdjustBalance(balanceChange);
            await _accountRepository.UpdateAsync(account, cancellationToken);

            await UnitOfWork.SaveChangesAsync(cancellationToken);

            // Update budget spending for expense transactions
            if (dto.Type == TransactionType.Expense)
            {
                var activeBudget = await _budgetService.GetActiveForCategoryAsync(
                    userId, dto.CategoryId, dto.Date, cancellationToken);
                if (activeBudget != null)
                {
                    await _budgetService.AddSpendingAsync(activeBudget.Id, dto.Amount, userId, cancellationToken);
                }
            }

            Logger.LogInformation("Created transaction {TransactionId} for user {UserId}", transaction.Id, userId);
            return transaction.ToDto(account.Name, category.Name, category.Icon ?? "", category.Color ?? "");
        }, cancellationToken);
    }

    public async Task<TransactionDto> UpdateAsync(Guid id, UpdateTransactionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        return await ExecuteInTransactionAsync(async () =>
        {
            var transaction = await _transactionRepository.GetByIdAsync(id, cancellationToken)
                ?? throw new DomainExceptions.NotFoundException("Transaction", id);

            if (transaction.UserId != userId)
                throw new DomainExceptions.NotFoundException("Transaction", id);

            var oldAmount = transaction.Amount;
            var oldType = transaction.Type;
            var oldAccountId = transaction.AccountId;
            var oldCategoryId = transaction.CategoryId;

            if (dto.Amount != null)
                transaction.UpdateAmount(dto.Amount);

            if (dto.Date.HasValue)
                transaction.UpdateDate(dto.Date.Value);

            if (dto.Notes != null)
                transaction.UpdateNotes(dto.Notes);

            if (dto.AccountId.HasValue)
            {
                var newAccount = await _accountRepository.GetByIdAsync(dto.AccountId.Value.Value, cancellationToken)
                    ?? throw new DomainExceptions.NotFoundException("Account", dto.AccountId.Value.Value);

                if (newAccount.UserId != userId)
                    throw new DomainExceptions.NotFoundException("Account", dto.AccountId.Value.Value);

                transaction.UpdateAccount(dto.AccountId.Value);
            }

            if (dto.CategoryId.HasValue)
            {
                var newCategory = await _categoryRepository.GetByIdAsync(dto.CategoryId.Value.Value, cancellationToken)
                    ?? throw new DomainExceptions.NotFoundException("Category", dto.CategoryId.Value.Value);

                if (newCategory.UserId != userId)
                    throw new DomainExceptions.NotFoundException("Category", dto.CategoryId.Value.Value);

                var expectedCategoryType = transaction.Type == TransactionType.Expense ? CategoryType.Expense : CategoryType.Income;
                if (newCategory.Type != expectedCategoryType)
                    throw new DomainExceptions.ValidationException("Category type does not match transaction type", "CATEGORY_TYPE_MISMATCH");

                transaction.UpdateCategory(dto.CategoryId.Value);
            }

            transaction.MarkAsPendingUpdate();
            await _transactionRepository.UpdateAsync(transaction, cancellationToken);

            await AdjustAccountBalancesAsync(transaction, oldAmount, oldType, oldAccountId, oldCategoryId, dto, cancellationToken);

            await UnitOfWork.SaveChangesAsync(cancellationToken);

            var account = await _accountRepository.GetByIdAsync(transaction.AccountId.Value, cancellationToken);
            var cat = await _categoryRepository.GetByIdAsync(transaction.CategoryId.Value, cancellationToken);

            Logger.LogInformation("Updated transaction {TransactionId} for user {UserId}", transaction.Id, userId);
            return transaction.ToDto(
                account?.Name ?? "",
                cat?.Name ?? "",
                cat?.Icon ?? "",
                cat?.Color ?? "");
        }, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync(async () =>
        {
            var transaction = await _transactionRepository.GetByIdAsync(id, cancellationToken)
                ?? throw new DomainExceptions.NotFoundException("Transaction", id);

            if (transaction.UserId != userId)
                throw new DomainExceptions.NotFoundException("Transaction", id);

            var account = await _accountRepository.GetByIdAsync(transaction.AccountId.Value, cancellationToken);
            if (account != null && account.UserId == userId)
            {
                var balanceChange = transaction.Type == TransactionType.Income
                    ? new Money(-transaction.Amount.Amount, transaction.Amount.Currency)
                    : transaction.Amount;

                account.AdjustBalance(balanceChange);
                await _accountRepository.UpdateAsync(account, cancellationToken);
            }

            transaction.MarkAsDeleted();
            transaction.MarkAsPendingDelete();
            await _transactionRepository.UpdateAsync(transaction, cancellationToken);

            await UnitOfWork.SaveChangesAsync(cancellationToken);

            Logger.LogInformation("Deleted transaction {TransactionId} for user {UserId}", transaction.Id, userId);
        }, cancellationToken);
    }

    public async Task<TransactionDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var transaction = await _transactionRepository.GetByIdAsync(id, cancellationToken);
        if (transaction == null || transaction.UserId != userId)
            return null;

        var account = await _accountRepository.GetByIdAsync(transaction.AccountId.Value, cancellationToken);
        var cat = await _categoryRepository.GetByIdAsync(transaction.CategoryId.Value, cancellationToken);

        return transaction.ToDto(
            account?.Name ?? "",
            cat?.Name ?? "",
            cat?.Icon ?? "",
            cat?.Color ?? "");
    }

    public async Task<IReadOnlyList<TransactionDto>> GetAllAsync(Guid userId, TransactionFilterDto filter, CancellationToken cancellationToken = default)
    {
        await _filterValidator.ValidateAndThrowAsync(filter, cancellationToken);

        IReadOnlyList<Transaction> transactions;

        if (filter.StartDate.HasValue && filter.EndDate.HasValue && filter.Type.HasValue)
        {
            transactions = await _transactionRepository.GetByTypeAndDateRangeAsync(
                userId, filter.Type.Value, filter.StartDate.Value, filter.EndDate.Value, cancellationToken);
        }
        else if (filter.StartDate.HasValue && filter.EndDate.HasValue)
        {
            transactions = await _transactionRepository.GetByDateRangeAsync(
                userId, filter.StartDate.Value, filter.EndDate.Value, cancellationToken);
        }
        else if (filter.Type.HasValue)
        {
            transactions = await _transactionRepository.GetByTypeAsync(userId, filter.Type.Value, cancellationToken);
        }
        else if (filter.AccountId.HasValue)
        {
            transactions = await _transactionRepository.GetByAccountIdAsync(userId, filter.AccountId.Value, cancellationToken);
        }
        else if (filter.CategoryId.HasValue)
        {
            transactions = await _transactionRepository.GetByCategoryIdAsync(userId, filter.CategoryId.Value, cancellationToken);
        }
        else
        {
            transactions = await _transactionRepository.GetByUserIdAsync(userId, cancellationToken);
        }

        var result = transactions
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        var accountIds = result.Select(t => t.AccountId.Value).Distinct().ToList();
        var categoryIds = result.Select(t => t.CategoryId.Value).Distinct().ToList();

        var accounts = new Dictionary<Guid, Account>();
        foreach (var accId in accountIds)
        {
            var acc = await _accountRepository.GetByIdAsync(accId, cancellationToken);
            if (acc != null) accounts[accId] = acc;
        }

        var categories = new Dictionary<Guid, Category>();
        foreach (var catId in categoryIds)
        {
            var cat = await _categoryRepository.GetByIdAsync(catId, cancellationToken);
            if (cat != null) categories[catId] = cat;
        }

        return result.Select(t => t.ToDto(
            accounts.TryGetValue(t.AccountId.Value, out var acc) ? acc.Name : "",
            categories.TryGetValue(t.CategoryId.Value, out var cat) ? cat.Name : "",
            categories.TryGetValue(t.CategoryId.Value, out cat) ? cat.Icon ?? "" : "",
            categories.TryGetValue(t.CategoryId.Value, out cat) ? cat.Color ?? "" : ""
        )).ToList();
    }

    public async Task<TransactionSummaryDto> GetSummaryAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var totalIncome = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Income, startDate, endDate, cancellationToken);
        var totalExpense = await _transactionRepository.GetTotalByTypeAsync(userId, TransactionType.Expense, startDate, endDate, cancellationToken);

        var transactions = await _transactionRepository.GetByDateRangeAsync(userId, startDate, endDate, cancellationToken);

        return new TransactionSummaryDto(
            totalIncome,
            totalExpense,
            totalIncome.Subtract(totalExpense),
            transactions.Count(t => !t.IsDeleted));
    }

    public async Task<IReadOnlyList<TransactionDto>> GetRecentAsync(Guid userId, int count, CancellationToken cancellationToken = default)
    {
        var transactions = await _transactionRepository.GetRecentAsync(userId, count, cancellationToken);
        var accountIds = transactions.Select(t => t.AccountId.Value).Distinct().ToList();
        var categoryIds = transactions.Select(t => t.CategoryId.Value).Distinct().ToList();

        var accounts = new Dictionary<Guid, Account>();
        foreach (var accId in accountIds)
        {
            var acc = await _accountRepository.GetByIdAsync(accId, cancellationToken);
            if (acc != null) accounts[accId] = acc;
        }

        var categories = new Dictionary<Guid, Category>();
        foreach (var catId in categoryIds)
        {
            var cat = await _categoryRepository.GetByIdAsync(catId, cancellationToken);
            if (cat != null) categories[catId] = cat;
        }

        return transactions.Select(t => t.ToDto(
            accounts.TryGetValue(t.AccountId.Value, out var acc) ? acc.Name : "",
            categories.TryGetValue(t.CategoryId.Value, out var cat) ? cat.Name : "",
            categories.TryGetValue(t.CategoryId.Value, out cat) ? cat.Icon ?? "" : "",
            categories.TryGetValue(t.CategoryId.Value, out cat) ? cat.Color ?? "" : ""
        )).ToList();
    }

    public async Task<Money> GetTotalByCategoryAsync(Guid userId, CategoryId categoryId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _transactionRepository.GetTotalByCategoryAsync(userId, categoryId, startDate, endDate, cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionDto>> GetByAccountAsync(Guid userId, AccountId accountId, CancellationToken cancellationToken = default)
    {
        var transactions = await _transactionRepository.GetByAccountIdAsync(userId, accountId, cancellationToken);

        var categoryIds = transactions.Select(t => t.CategoryId.Value).Distinct().ToList();
        var categories = new Dictionary<Guid, Category>();
        foreach (var catId in categoryIds)
        {
            var cat = await _categoryRepository.GetByIdAsync(catId, cancellationToken);
            if (cat != null) categories[catId] = cat;
        }

        var account = await _accountRepository.GetByIdAsync(accountId.Value, cancellationToken);

        return transactions.Select(t => t.ToDto(
            account?.Name ?? "",
            categories.TryGetValue(t.CategoryId.Value, out var cat) ? cat.Name : "",
            categories.TryGetValue(t.CategoryId.Value, out cat) ? cat.Icon ?? "" : "",
            categories.TryGetValue(t.CategoryId.Value, out cat) ? cat.Color ?? "" : ""
        )).ToList();
    }

    private async Task ValidateTransactionAsync(CreateTransactionDto dto, Guid userId, CancellationToken cancellationToken)
    {
        var account = await _accountRepository.GetByIdAsync(dto.AccountId.Value, cancellationToken);
        if (account == null || account.UserId != userId)
            throw new DomainExceptions.NotFoundException("Account", dto.AccountId.Value);

        var category = await _categoryRepository.GetByIdAsync(dto.CategoryId.Value, cancellationToken);
        if (category == null || category.UserId != userId)
            throw new DomainExceptions.NotFoundException("Category", dto.CategoryId.Value);

        if (!category.IsActive)
            throw new DomainExceptions.ValidationException("Category is not active", "CATEGORY_INACTIVE");
    }

    private async Task AdjustAccountBalancesAsync(
        Transaction transaction,
        Money oldAmount,
        TransactionType oldType,
        AccountId oldAccountId,
        CategoryId oldCategoryId,
        UpdateTransactionDto dto,
        CancellationToken cancellationToken)
    {
        var oldAccount = await _accountRepository.GetByIdAsync(oldAccountId.Value, cancellationToken);
        if (oldAccount != null)
        {
            var oldBalanceChange = oldType == TransactionType.Income ? oldAmount : new Money(-oldAmount.Amount, oldAmount.Currency);
            var newBalanceChange = transaction.Type == TransactionType.Income ? transaction.Amount : new Money(-transaction.Amount.Amount, transaction.Amount.Currency);

            var netChange = newBalanceChange.Subtract(oldBalanceChange);
            oldAccount.AdjustBalance(netChange);
            await _accountRepository.UpdateAsync(oldAccount, cancellationToken);
        }

        if (dto.AccountId.HasValue && dto.AccountId.Value != oldAccountId)
        {
            var newAccount = await _accountRepository.GetByIdAsync(dto.AccountId.Value.Value, cancellationToken);
            if (newAccount != null)
            {
                var newBalanceChange = transaction.Type == TransactionType.Income ? transaction.Amount : new Money(-transaction.Amount.Amount, transaction.Amount.Currency);
                newAccount.AdjustBalance(newBalanceChange);
                await _accountRepository.UpdateAsync(newAccount, cancellationToken);
            }
        }
    }
}