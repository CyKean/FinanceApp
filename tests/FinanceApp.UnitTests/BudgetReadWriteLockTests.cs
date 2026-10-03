namespace FinanceApp.UnitTests;

using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Application.Validators;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

/// <summary>
/// Opening Budgets, Budget Ideas or Forecasts calls
/// <c>IBudgetService.GetActiveAsync</c>, which recomputes each budget's spent
/// total. That used to end in an unconditional <c>SaveChangesAsync</c>, so simply
/// viewing a page took SQLite's write lock.
/// <para>
/// The lock is what produced "Paytin isn't responding" on back: the AI page's
/// background pipeline held the write lock while the page being returned to
/// reloaded synchronously on the UI thread, which then sat waiting on it. These
/// tests pin the guarantee that a read which changes nothing performs no write.
/// </para>
/// </summary>
public class BudgetReadWriteLockTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IBudgetRepository> _budgets = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly BudgetService _service;

    public BudgetReadWriteLockTests()
    {
        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _service = new BudgetService(
            _unitOfWork.Object,
            _budgets.Object,
            _categories.Object,
            Mock.Of<IAccountRepository>(),
            _transactions.Object,
            Mock.Of<INotificationService>(),
            new CreateBudgetDtoValidator(),
            new UpdateBudgetDtoValidator(),
            Mock.Of<ILogger<BudgetService>>());
    }

    private (Budget Budget, Guid UserId) ArrangeBudget(Money storedSpent, Money computedSpent)
    {
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var budget = new Budget(
            "Food Budget", new Money(1000),
            new DateTime(2024, 1, 1), new DateTime(2024, 1, 31),
            new CategoryId(categoryId), userId);

        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(budget, Guid.NewGuid());

        if (storedSpent.Amount > 0) budget.AddSpending(storedSpent);

        var category = new Category("Food", CategoryType.Expense, userId, "tag", "#FF6B6B", null, false, 0);
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(category, categoryId);

        _budgets
            .Setup(x => x.GetActiveByUserIdAsync(userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Budget> { budget });

        _categories
            .Setup(x => x.GetByIdAsync(new CategoryId(categoryId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        _transactions
            .Setup(x => x.GetTotalByCategoryAsync(
                userId, new CategoryId(categoryId), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(computedSpent);

        return (budget, userId);
    }

    [Fact]
    public async Task GetActiveAsync_DoesNotWrite_WhenSpentAmountIsUnchanged()
    {
        var (_, userId) = ArrangeBudget(storedSpent: new Money(350), computedSpent: new Money(350));

        var result = await _service.GetActiveAsync(userId, new DateTime(2024, 1, 15));

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "An unchanged spent total must not take SQLite's write lock.");

        // The read still has to report the right figures.
        Assert.Equal(350, Assert.Single(result).SpentAmount.Amount);
    }

    [Fact]
    public async Task GetActiveAsync_Writes_WhenSpentAmountMoved()
    {
        var (_, userId) = ArrangeBudget(storedSpent: Money.Zero(), computedSpent: new Money(350));

        await _service.GetActiveAsync(userId, new DateTime(2024, 1, 15));

        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}