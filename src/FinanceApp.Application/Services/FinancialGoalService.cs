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

public class FinancialGoalService : BaseService, IFinancialGoalService
{
    private readonly IFinancialGoalRepository _goalRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly CreateFinancialGoalDtoValidator _createValidator;
    private readonly UpdateFinancialGoalDtoValidator _updateValidator;
    private readonly GoalProgressDtoValidator _progressValidator;

    public FinancialGoalService(
        IUnitOfWork unitOfWork,
        IFinancialGoalRepository goalRepository,
        IAccountRepository accountRepository,
        CreateFinancialGoalDtoValidator createValidator,
        UpdateFinancialGoalDtoValidator updateValidator,
        GoalProgressDtoValidator progressValidator,
        ILogger<FinancialGoalService> logger) : base(unitOfWork, logger)
    {
        _goalRepository = goalRepository;
        _accountRepository = accountRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _progressValidator = progressValidator;
    }

    public async Task<FinancialGoalDto> CreateAsync(CreateFinancialGoalDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        if (dto.LinkedAccountId.HasValue)
        {
            var account = await _accountRepository.GetByIdAsync(dto.LinkedAccountId.Value.Value, cancellationToken);
            if (account == null || account.UserId != userId)
                throw new DomainExceptions.NotFoundException("Account", dto.LinkedAccountId.Value.Value);
        }

        var goal = new FinancialGoal(
            dto.Name,
            dto.TargetAmount,
            dto.TargetDate,
            userId,
            dto.StartDate,
            dto.Description,
            dto.Icon,
            dto.Color,
            dto.LinkedAccountId);

        await _goalRepository.AddAsync(goal, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Created financial goal {GoalId} for user {UserId}", goal.Id, userId);
        return goal.ToDto();
    }

    public async Task<FinancialGoalDto> UpdateAsync(Guid id, UpdateFinancialGoalDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var goal = await _goalRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        if (goal.UserId != userId)
            throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        if (dto.Name != null)
            goal.UpdateName(dto.Name);

        if (dto.TargetAmount != null)
            goal.UpdateTargetAmount(dto.TargetAmount);

        if (dto.TargetDate.HasValue)
            goal.UpdateTargetDate(dto.TargetDate.Value);

        if (dto.Description != null)
            goal.UpdateDescription(dto.Description);

        if (dto.Icon != null)
            goal.UpdateIcon(dto.Icon);

        if (dto.Color != null)
            goal.UpdateColor(dto.Color);

        if (dto.LinkedAccountId.HasValue)
        {
            if (dto.LinkedAccountId.Value != Guid.Empty)
            {
                var linkedAccount = await _accountRepository.GetByIdAsync(dto.LinkedAccountId.Value.Value, cancellationToken);
                if (linkedAccount == null || linkedAccount.UserId != userId)
                    throw new DomainExceptions.NotFoundException("Account", dto.LinkedAccountId.Value.Value);
            }
            goal.UpdateLinkedAccount(dto.LinkedAccountId.Value);
        }

        if (dto.Status.HasValue)
        {
            switch (dto.Status.Value)
            {
                case GoalStatus.Active:
                    goal.Reactivate();
                    break;
                case GoalStatus.Paused:
                    goal.Pause();
                    break;
                case GoalStatus.Completed:
                    goal.Complete();
                    break;
                case GoalStatus.Cancelled:
                    goal.Cancel();
                    break;
            }
        }

        goal.MarkAsPendingUpdate();
        await _goalRepository.UpdateAsync(goal, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Updated financial goal {GoalId} for user {UserId}", goal.Id, userId);

        var acc = goal.LinkedAccountId.HasValue
            ? await _accountRepository.GetByIdAsync(goal.LinkedAccountId.Value.Value, cancellationToken)
            : null;

        return goal.ToDto(acc?.Name);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        if (goal.UserId != userId)
            throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        goal.MarkAsDeleted();
        goal.MarkAsPendingDelete();
        await _goalRepository.UpdateAsync(goal, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Deleted financial goal {GoalId} for user {UserId}", goal.Id, userId);
    }

    public async Task<FinancialGoalDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepository.GetByIdAsync(id, cancellationToken);
        if (goal == null || goal.UserId != userId)
            return null;

        var acc = goal.LinkedAccountId.HasValue
            ? await _accountRepository.GetByIdAsync(goal.LinkedAccountId.Value.Value, cancellationToken)
            : null;

        return goal.ToDto(acc?.Name);
    }

    public async Task<IReadOnlyList<FinancialGoalDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var goals = await _goalRepository.GetByUserIdAsync(userId, cancellationToken);
        var accounts = await LoadLinkedAccountsAsync(goals, cancellationToken);

        return goals.Select(g => g.ToDto(LinkedAccountName(g, accounts))).ToList();
    }

    public async Task<IReadOnlyList<FinancialGoalDto>> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var goals = await _goalRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        var accounts = await LoadLinkedAccountsAsync(goals, cancellationToken);

        return goals.Select(g => g.ToDto(LinkedAccountName(g, accounts))).ToList();
    }

    /// <summary>
    /// One query for every linked account rather than one per linked goal.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, Account>> LoadLinkedAccountsAsync(
        IReadOnlyList<FinancialGoal> goals,
        CancellationToken cancellationToken)
    {
        var accountIds = goals
            .Where(g => g.LinkedAccountId.HasValue)
            .Select(g => g.LinkedAccountId!.Value.Value)
            .Distinct()
            .ToList();

        if (accountIds.Count == 0)
            return new Dictionary<Guid, Account>();

        return await _accountRepository.GetByIdsAsync(accountIds, cancellationToken);
    }

    private static string? LinkedAccountName(FinancialGoal goal, IReadOnlyDictionary<Guid, Account> accounts) =>
        goal.LinkedAccountId.HasValue &&
        accounts.TryGetValue(goal.LinkedAccountId.Value.Value, out var account)
            ? account.Name
            : null;

    public async Task AddProgressAsync(Guid id, GoalProgressDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        await _progressValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var goal = await _goalRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        if (goal.UserId != userId)
            throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        goal.AddProgress(dto.Amount);
        goal.MarkAsPendingUpdate();
        await _goalRepository.UpdateAsync(goal, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Added progress to goal {GoalId} for user {UserId}", goal.Id, userId);
    }

    public async Task RemoveProgressAsync(Guid id, Money amount, Guid userId, CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        if (goal.UserId != userId)
            throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        goal.RemoveProgress(amount);
        goal.MarkAsPendingUpdate();
        await _goalRepository.UpdateAsync(goal, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Removed progress from goal {GoalId} for user {UserId}", goal.Id, userId);
    }

    public async Task CompleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        if (goal.UserId != userId)
            throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        goal.Complete();
        goal.MarkAsPendingUpdate();
        await _goalRepository.UpdateAsync(goal, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Completed goal {GoalId} for user {UserId}", goal.Id, userId);
    }

    public async Task PauseAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        if (goal.UserId != userId)
            throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        goal.Pause();
        goal.MarkAsPendingUpdate();
        await _goalRepository.UpdateAsync(goal, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ReactivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        if (goal.UserId != userId)
            throw new DomainExceptions.NotFoundException("FinancialGoal", id);

        goal.Reactivate();
        goal.MarkAsPendingUpdate();
        await _goalRepository.UpdateAsync(goal, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}