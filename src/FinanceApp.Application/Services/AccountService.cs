namespace FinanceApp.Application.Services;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Validators;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.Exceptions;
using DomainExceptions = FinanceApp.Domain.Exceptions;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Application.Mappings;
using FluentValidation;
using Microsoft.Extensions.Logging;

public class AccountService : BaseService, IAccountService
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IFinancialGoalRepository _goalRepository;
    private readonly ICategoryService _categoryService;
    private readonly CreateAccountDtoValidator _createValidator;
    private readonly UpdateAccountDtoValidator _updateValidator;

public AccountService(
        IUnitOfWork unitOfWork,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IBudgetRepository budgetRepository,
        IFinancialGoalRepository goalRepository,
        ICategoryService categoryService,
        CreateAccountDtoValidator createValidator,
        UpdateAccountDtoValidator updateValidator,
        ILogger<AccountService> logger) : base(unitOfWork, logger)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _budgetRepository = budgetRepository;
        _goalRepository = goalRepository;
        _categoryService = categoryService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<AccountDto> CreateAsync(CreateAccountDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var existingDefault = await _accountRepository.GetDefaultAccountAsync(userId, cancellationToken);
        var isDefault = dto.IsDefault || existingDefault == null;

        if (dto.IsDefault && existingDefault != null)
        {
            existingDefault.UnsetAsDefault();
            await _accountRepository.UpdateAsync(existingDefault, cancellationToken);
        }

        var account = new Account(
            dto.Name,
            dto.Type,
            dto.InitialBalance,
            userId,
            dto.Description,
            dto.Icon,
            dto.Color,
            isDefault);

        await _accountRepository.AddAsync(account, cancellationToken);

        // Default categories are an app default, not demo content, and a user
        // cannot record anything without one. They used to arrive as a side
        // effect of the demo seeder, which left a brand new account with no
        // categories at all once that was switched off. Idempotent, so this is
        // cheap on every subsequent account.
        await _categoryService.InitializeDefaultCategoriesAsync(userId, cancellationToken);

        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Created account {AccountId} for user {UserId}", account.Id, userId);
        return account.ToDto();
    }

    public async Task<AccountDto> UpdateAsync(Guid id, UpdateAccountDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var account = await _accountRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Account", id);

        if (account.UserId != userId)
            throw new NotFoundException("Account", id);

        if (dto.Name != null)
            account.UpdateName(dto.Name);

        if (dto.Type.HasValue)
            account.UpdateType(dto.Type.Value);

        if (dto.Description != null)
            account.UpdateDescription(dto.Description);

        if (dto.Icon != null)
            account.UpdateIcon(dto.Icon);

        if (dto.Color != null)
            account.UpdateColor(dto.Color);

        if (dto.IsDefault.HasValue && dto.IsDefault.Value && !account.IsDefault)
        {
            var existingDefault = await _accountRepository.GetDefaultAccountAsync(userId, cancellationToken);
            if (existingDefault != null && existingDefault.Id != id)
            {
                existingDefault.UnsetAsDefault();
                await _accountRepository.UpdateAsync(existingDefault, cancellationToken);
            }
            account.SetAsDefault();
        }

        if (dto.SortOrder.HasValue)
            account.UpdateSortOrder(dto.SortOrder.Value);

        account.MarkAsPendingUpdate();
        await _accountRepository.UpdateAsync(account, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Updated account {AccountId} for user {UserId}", account.Id, userId);
        return account.ToDto();
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Account", id);

        if (account.UserId != userId)
            throw new NotFoundException("Account", id);

        // Connection guard: transactions live and die with their account.
        var accountId = new AccountId(id);
        var transactions = await _transactionRepository.GetByAccountIdAsync(userId, accountId, cancellationToken);
        if (transactions.Any())
            throw new DomainExceptions.ValidationException(
                $"Cannot delete '{account.Name}' because it has {transactions.Count} transaction(s). Delete or move them first.",
                "ACCOUNT_IN_USE");

        // Unlink budgets and goals so they don't point at a deleted account.
        var budgets = await _budgetRepository.GetByUserIdAsync(userId, cancellationToken);
        foreach (var budget in budgets.Where(b => b.LinkedAccountId.HasValue && b.LinkedAccountId.Value == accountId))
        {
            budget.UpdateLinkedAccount(null);
            await _budgetRepository.UpdateAsync(budget, cancellationToken);
        }

        var goals = await _goalRepository.GetByUserIdAsync(userId, cancellationToken);
        foreach (var goal in goals.Where(g => g.LinkedAccountId.HasValue && g.LinkedAccountId.Value == accountId))
        {
            goal.UpdateLinkedAccount(null);
            await _goalRepository.UpdateAsync(goal, cancellationToken);
        }

        // Promote the oldest remaining account when deleting the default.
        if (account.IsDefault)
        {
            var successor = (await _accountRepository.GetByUserIdAsync(userId, cancellationToken))
                .Where(a => a.Id != id && !a.IsDeleted)
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.CreatedAt)
                .FirstOrDefault();
            if (successor != null)
            {
                successor.SetAsDefault();
                await _accountRepository.UpdateAsync(successor, cancellationToken);
            }
        }

        account.MarkAsDeleted();
        account.MarkAsPendingDelete();
        await _accountRepository.UpdateAsync(account, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Deleted account {AccountId} for user {UserId}", account.Id, userId);
    }

    public async Task<AccountDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(id, cancellationToken);
        if (account == null || account.UserId != userId)
            return null;

        return account.ToDto();
    }

    public async Task<IReadOnlyList<AccountDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var accounts = await _accountRepository.GetByUserIdAsync(userId, cancellationToken);
        return accounts.Select(a => a.ToDto()).ToList();
    }

    public async Task<AccountDto?> GetDefaultAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetDefaultAccountAsync(userId, cancellationToken);
        return account?.ToDto();
    }

    public async Task<IReadOnlyList<AccountDto>> GetByTypeAsync(Guid userId, AccountType type, CancellationToken cancellationToken = default)
    {
        var accounts = await _accountRepository.GetByTypeAsync(userId, type, cancellationToken);
        return accounts.Select(a => a.ToDto()).ToList();
    }

    public async Task<Money> GetTotalBalanceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _accountRepository.GetTotalBalanceAsync(userId, cancellationToken);
    }

    public async Task SetDefaultAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Account", id);

        if (account.UserId != userId)
            throw new NotFoundException("Account", id);

        var existingDefault = await _accountRepository.GetDefaultAccountAsync(userId, cancellationToken);
        if (existingDefault != null && existingDefault.Id != id)
        {
            existingDefault.UnsetAsDefault();
            await _accountRepository.UpdateAsync(existingDefault, cancellationToken);
        }

        account.SetAsDefault();
        account.MarkAsPendingUpdate();
        await _accountRepository.UpdateAsync(account, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Set account {AccountId} as default for user {UserId}", account.Id, userId);
    }
}