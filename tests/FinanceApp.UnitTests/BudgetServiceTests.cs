using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Application.Validators;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceApp.UnitTests;

public class BudgetServiceTests
{
    private readonly Mock<IBudgetRepository> _mockBudgetRepository;
    private readonly Mock<ICategoryRepository> _mockCategoryRepository;
    private readonly Mock<ITransactionRepository> _mockTransactionRepository;
    private readonly Mock<ILogger<BudgetService>> _mockLogger;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly CreateBudgetDtoValidator _createValidator;
    private readonly UpdateBudgetDtoValidator _updateValidator;
    private readonly BudgetService _budgetService;

    public BudgetServiceTests()
    {
        _mockBudgetRepository = new Mock<IBudgetRepository>();
        _mockCategoryRepository = new Mock<ICategoryRepository>();
        _mockTransactionRepository = new Mock<ITransactionRepository>();
        _mockLogger = new Mock<ILogger<BudgetService>>();
        _mockNotificationService = new Mock<INotificationService>();

        _createValidator = new CreateBudgetDtoValidator();
        _updateValidator = new UpdateBudgetDtoValidator();

        var mockUnitOfWork = Mock.Of<IUnitOfWork>();
        Mock.Get(mockUnitOfWork)
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _budgetService = new BudgetService(
            Mock.Of<IUnitOfWork>(),
            _mockBudgetRepository.Object,
            _mockCategoryRepository.Object,
            Mock.Of<IAccountRepository>(),
            _mockTransactionRepository.Object,
            Mock.Of<INotificationService>(),
            new CreateBudgetDtoValidator(),
            new UpdateBudgetDtoValidator(),
            Mock.Of<ILogger<BudgetService>>());
    }

    [Fact]
    public async Task CreateAsync_CreatesBudget_WhenValidData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var category = new Category("Food", CategoryType.Expense, userId, "🍔", "#FF6B6B", null, false, 0);
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(category, categoryId);

        _mockCategoryRepository.Setup(x => x.GetByIdAsync(new CategoryId(categoryId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        
        _mockBudgetRepository.Setup(x => x.GetActiveForCategoryAsync(userId, new CategoryId(categoryId), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Budget?)null);

        // Setup GetTotalByCategoryAsync to match any userId
        _mockTransactionRepository.Setup(x => x.GetTotalByCategoryAsync(
            It.IsAny<Guid>(), 
            new CategoryId(categoryId), 
            It.IsAny<DateTime>(), 
            It.IsAny<DateTime>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Money(350));

        var dto = new CreateBudgetDto(
            "Food Budget",
            new Money(500),
            new DateTime(2024, 1, 1),
            new DateTime(2024, 1, 31),
            new CategoryId(categoryId));

        // Act
        var result = await _budgetService.CreateAsync(dto, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Food Budget", result.Name);
        Assert.Equal(500, result.Amount.Amount);
        Assert.Equal(categoryId, result.CategoryId.Value);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenCategoryNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        _mockCategoryRepository.Setup(x => x.GetByIdAsync(new CategoryId(categoryId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var dto = new CreateBudgetDto(
            "Food Budget",
            new Money(500),
            new DateTime(2024, 1, 1),
            new DateTime(2024, 1, 31),
            new CategoryId(categoryId));

        // Act & Assert
        await Assert.ThrowsAsync<FinanceApp.Domain.Exceptions.NotFoundException>(
            () => _budgetService.CreateAsync(dto, userId));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenCategoryNotExpense()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var category = new Category("Salary", CategoryType.Income, userId, "💼", "#28A745", null, false, 0);
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(category, categoryId);

        _mockCategoryRepository.Setup(x => x.GetByIdAsync(new CategoryId(categoryId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var dto = new CreateBudgetDto(
            "Salary Budget",
            new Money(5000),
            new DateTime(2024, 1, 1),
            new DateTime(2024, 1, 31),
            new CategoryId(categoryId));

        // Act & Assert
        await Assert.ThrowsAsync<FinanceApp.Domain.Exceptions.ValidationException>(
            () => _budgetService.CreateAsync(dto, userId));
    }

    [Fact]
    public async Task GetActiveAsync_CalculatesSpentAmount_FromTransactions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        
        var budget = new Budget("Food Budget", new Money(1000),
            new DateTime(2024, 1, 1), new DateTime(2024, 1, 31),
            new CategoryId(categoryId), userId);
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(budget, Guid.NewGuid());

        var category = new Category("Food", CategoryType.Expense, userId, "🍔", "#FF6B6B", null, false, 0);
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(category, categoryId);

        _mockBudgetRepository.Setup(x => x.GetActiveByUserIdAsync(userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Budget> { budget });
        
        _mockCategoryRepository.Setup(x => x.GetByIdAsync(new CategoryId(categoryId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        _mockTransactionRepository.Setup(x => x.GetTotalByCategoryAsync(
            userId, new CategoryId(categoryId), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Money(350));

        // Act
        var result = await _budgetService.GetActiveAsync(userId, new DateTime(2024, 1, 15));

        // Assert
        Assert.Single(result);
        var budgetDto = result.First();
        Assert.Equal(350, budgetDto.SpentAmount.Amount);
        Assert.Equal(650, budgetDto.RemainingAmount.Amount);
        Assert.Equal(35.0m, budgetDto.PercentageUsed);
    }
}