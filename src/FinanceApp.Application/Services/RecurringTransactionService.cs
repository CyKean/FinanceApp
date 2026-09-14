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

public class RecurringTransactionService : BaseService, IRecurringTransactionService
{
    private readonly IRecurringTransactionRepository _recurringRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly CreateRecurringTransactionDtoValidator _createValidator;
    private readonly UpdateRecurringTransactionDtoValidator _updateValidator;

    public RecurringTransactionService(
        IUnitOfWork unitOfWork,
        IRecurringTransactionRepository recurringRepository,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        CreateRecurringTransactionDtoValidator createValidator,
        UpdateRecurringTransactionDtoValidator updateValidator,
        ILogger<RecurringTransactionService> logger) : base(unitOfWork, logger)
    {
        _recurringRepository = recurringRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
        _transactionRepository = transactionRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<RecurringTransactionDto> CreateAsync(CreateRecurringTransactionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        await ValidateRecurringTransactionAsync(dto, userId, cancellationToken);

        var recurring = new RecurringTransaction(
            dto.Name,
            dto.Type,
            dto.Amount,
            dto.Frequency,
            dto.StartDate,
            dto.AccountId,
            dto.CategoryId,
            userId,
            dto.Notes,
            dto.EndDate);

        await _recurringRepository.AddAsync(recurring, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Created recurring transaction {RecurringId} for user {UserId}", recurring.Id, userId);

        var account = await _accountRepository.GetByIdAsync(recurring.AccountId.Value, cancellationToken);
        var category = await _categoryRepository.GetByIdAsync(recurring.CategoryId.Value, cancellationToken);

        return recurring.ToDto(account?.Name ?? "", category?.Name ?? "");
    }

    public async Task<RecurringTransactionDto> UpdateAsync(Guid id, UpdateRecurringTransactionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var recurring = await _recurringRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("RecurringTransaction", id);

        if (recurring.UserId != userId)
            throw new DomainExceptions.NotFoundException("RecurringTransaction", id);

        if (dto.Name != null)
            recurring.UpdateName(dto.Name);

        if (dto.Amount != null)
            recurring.UpdateAmount(dto.Amount);

        if (dto.Frequency.HasValue)
            recurring.UpdateFrequency(dto.Frequency.Value);

        if (dto.StartDate.HasValue || dto.EndDate.HasValue)
        {
            var startDate = dto.StartDate ?? recurring.StartDate;
            var endDate = dto.EndDate ?? recurring.EndDate;
            recurring.UpdateDates(startDate, endDate);
        }

        if (dto.AccountId.HasValue)
        {
            var acc = await _accountRepository.GetByIdAsync(dto.AccountId.Value.Value, cancellationToken)
                ?? throw new DomainExceptions.NotFoundException("Account", dto.AccountId.Value.Value);

            if (acc.UserId != userId)
                throw new DomainExceptions.NotFoundException("Account", dto.AccountId.Value.Value);

            recurring.UpdateAccount(dto.AccountId.Value);
        }

        if (dto.CategoryId.HasValue)
        {
            var cat = await _categoryRepository.GetByIdAsync(dto.CategoryId.Value.Value, cancellationToken)
                ?? throw new DomainExceptions.NotFoundException("Category", dto.CategoryId.Value.Value);

            if (cat.UserId != userId)
                throw new DomainExceptions.NotFoundException("Category", dto.CategoryId.Value.Value);

            recurring.UpdateCategory(dto.CategoryId.Value);
        }

        if (dto.Notes != null)
            recurring.UpdateNotes(dto.Notes);

        if (dto.IsActive.HasValue)
        {
            if (dto.IsActive.Value)
                recurring.Activate();
            else
                recurring.Deactivate();
        }

        recurring.MarkAsPendingUpdate();
        await _recurringRepository.UpdateAsync(recurring, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Updated recurring transaction {RecurringId} for user {UserId}", recurring.Id, userId);

        var account = await _accountRepository.GetByIdAsync(recurring.AccountId.Value, cancellationToken);
        var category = await _categoryRepository.GetByIdAsync(recurring.CategoryId.Value, cancellationToken);

        return recurring.ToDto(account?.Name ?? "", category?.Name ?? "");
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var recurring = await _recurringRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("RecurringTransaction", id);

        if (recurring.UserId != userId)
            throw new DomainExceptions.NotFoundException("RecurringTransaction", id);

        recurring.MarkAsDeleted();
        recurring.MarkAsPendingDelete();
        await _recurringRepository.UpdateAsync(recurring, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Deleted recurring transaction {RecurringId} for user {UserId}", recurring.Id, userId);
    }

    public async Task<RecurringTransactionDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var recurring = await _recurringRepository.GetByIdAsync(id, cancellationToken);
        if (recurring == null || recurring.UserId != userId)
            return null;

        var account = await _accountRepository.GetByIdAsync(recurring.AccountId.Value, cancellationToken);
        var category = await _categoryRepository.GetByIdAsync(recurring.CategoryId.Value, cancellationToken);

        return recurring.ToDto(account?.Name ?? "", category?.Name ?? "");
    }

    public async Task<IReadOnlyList<RecurringTransactionDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var recurrings = await _recurringRepository.GetByUserIdAsync(userId, cancellationToken);
        var accountIds = recurrings.Select(r => r.AccountId.Value).Distinct().ToList();
        var categoryIds = recurrings.Select(r => r.CategoryId.Value).Distinct().ToList();

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

        return recurrings.Select(r => r.ToDto(
            accounts.TryGetValue(r.AccountId.Value, out var acc) ? acc.Name : "",
            categories.TryGetValue(r.CategoryId.Value, out var cat) ? cat.Name : ""
        )).ToList();
    }

    public async Task<IReadOnlyList<RecurringTransactionDto>> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var recurrings = await _recurringRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        var accountIds = recurrings.Select(r => r.AccountId.Value).Distinct().ToList();
        var categoryIds = recurrings.Select(r => r.CategoryId.Value).Distinct().ToList();

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

        return recurrings.Select(r => r.ToDto(
            accounts.TryGetValue(r.AccountId.Value, out var acc) ? acc.Name : "",
            categories.TryGetValue(r.CategoryId.Value, out var cat) ? cat.Name : ""
        )).ToList();
    }

    public async Task<IReadOnlyList<RecurringTransactionDto>> GetDueAsync(Guid userId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var recurrings = await _recurringRepository.GetDueTransactionsAsync(userId, asOfDate, cancellationToken);
        var accountIds = recurrings.Select(r => r.AccountId.Value).Distinct().ToList();
        var categoryIds = recurrings.Select(r => r.CategoryId.Value).Distinct().ToList();

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

        return recurrings.Select(r => r.ToDto(
            accounts.TryGetValue(r.AccountId.Value, out var acc) ? acc.Name : "",
            categories.TryGetValue(r.CategoryId.Value, out var cat) ? cat.Name : ""
        )).ToList();
    }

    public async Task ProcessDueTransactionsAsync(Guid userId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var dueTransactions = await _recurringRepository.GetDueTransactionsAsync(userId, asOfDate, cancellationToken);

        foreach (var recurring in dueTransactions)
        {
            if (!recurring.IsActive)
                continue;

            if (recurring.EndDate.HasValue && asOfDate.Date > recurring.EndDate.Value.Date)
                continue;

            var transaction = recurring.GenerateTransaction(asOfDate);

            var account = await _accountRepository.GetByIdAsync(transaction.AccountId.Value, cancellationToken);
            if (account == null || account.UserId != userId)
                continue;

            var category = await _categoryRepository.GetByIdAsync(transaction.CategoryId.Value, cancellationToken);
            if (category == null || category.UserId != userId)
                continue;

            await _transactionRepository.AddAsync(transaction, cancellationToken);

            var balanceChange = transaction.Type == TransactionType.Income
                ? transaction.Amount
                : new Money(-transaction.Amount.Amount, transaction.Amount.Currency);

            account.AdjustBalance(balanceChange);
            await _accountRepository.UpdateAsync(account, cancellationToken);

            recurring.RecordGeneration(asOfDate);
            recurring.MarkAsPendingUpdate();
            await _recurringRepository.UpdateAsync(recurring, cancellationToken);

            Logger.LogInformation("Generated transaction {TransactionId} from recurring {RecurringId} for user {UserId}",
                transaction.Id, recurring.Id, userId);
        }

        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var recurring = await _recurringRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("RecurringTransaction", id);

        if (recurring.UserId != userId)
            throw new DomainExceptions.NotFoundException("RecurringTransaction", id);

        recurring.Activate();
        recurring.MarkAsPendingUpdate();
        await _recurringRepository.UpdateAsync(recurring, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var recurring = await _recurringRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("RecurringTransaction", id);

        if (recurring.UserId != userId)
            throw new DomainExceptions.NotFoundException("RecurringTransaction", id);

        recurring.Deactivate();
        recurring.MarkAsPendingUpdate();
        await _recurringRepository.UpdateAsync(recurring, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateRecurringTransactionAsync(CreateRecurringTransactionDto dto, Guid userId, CancellationToken cancellationToken)
    {
        var account = await _accountRepository.GetByIdAsync(dto.AccountId.Value, cancellationToken);
        if (account == null || account.UserId != userId)
            throw new DomainExceptions.NotFoundException("Account", dto.AccountId.Value);

        var category = await _categoryRepository.GetByIdAsync(dto.CategoryId.Value, cancellationToken);
        if (category == null || category.UserId != userId)
            throw new DomainExceptions.NotFoundException("Category", dto.CategoryId.Value);

        var expectedCategoryType = dto.Type == TransactionType.Expense ? CategoryType.Expense : CategoryType.Income;
        if (category.Type != expectedCategoryType)
            throw new DomainExceptions.ValidationException("Category type does not match transaction type", "CATEGORY_TYPE_MISMATCH");
    }
}