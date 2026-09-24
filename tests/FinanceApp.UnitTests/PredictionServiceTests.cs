using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceApp.UnitTests;

public class PredictionServiceTests
{
    private readonly Mock<ITransactionRepository> _mockTransactionRepository;
    private readonly Mock<ICategoryRepository> _mockCategoryRepository;
    private readonly Mock<IBudgetRepository> _mockBudgetRepository;
    private readonly Mock<IRecurringTransactionRepository> _mockRecurringRepository;
    private readonly Mock<ILogger<PredictionService>> _mockLogger;
    private readonly PredictionService _predictionService;

    public PredictionServiceTests()
    {
        _mockTransactionRepository = new Mock<ITransactionRepository>();
        _mockCategoryRepository = new Mock<ICategoryRepository>();
        _mockBudgetRepository = new Mock<IBudgetRepository>();
        _mockRecurringRepository = new Mock<IRecurringTransactionRepository>();
        _mockLogger = new Mock<ILogger<PredictionService>>();

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _predictionService = new PredictionService(
            mockUnitOfWork.Object,
            _mockTransactionRepository.Object,
            _mockCategoryRepository.Object,
            _mockBudgetRepository.Object,
            _mockRecurringRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task PredictExpensesAsync_ReturnsInsufficientData_WhenNoTransactions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockTransactionRepository.Setup(x => x.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transaction>());

        // Act
        var result = await _predictionService.PredictExpensesAsync(userId, 1);

        // Assert
        Assert.Equal(PredictionConfidence.InsufficientData, result.Confidence);
        Assert.Equal(0, result.MonthsAnalyzed);
        Assert.Equal(0, result.TotalTransactionsAnalyzed);
        Assert.Empty(result.CategoryPredictions);
    }

    [Fact]
    public async Task PredictExpensesAsync_ReturnsInsufficientData_WhenLessThanMinMonths()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var transactions = new List<Transaction>
        {
            CreateTransaction(Guid.NewGuid(), userId, 100, "Food", new DateTime(2024, 1, 15)),
            CreateTransaction(Guid.NewGuid(), userId, 50, "Transport", new DateTime(2024, 1, 20)),
        };
        
        _mockTransactionRepository.Setup(x => x.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        // Act
        var result = await _predictionService.PredictExpensesAsync(userId, 1);

        // Assert
        Assert.Equal(PredictionConfidence.InsufficientData, result.Confidence);
    }

    [Fact]
    public async Task PredictExpensesAsync_ReturnsPrediction_WhenSufficientData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var transactions = new List<Transaction>();
        
        // Create 12 months of data with 3+ transactions per month
        for (int month = 0; month < 12; month++)
        {
            var date = new DateTime(2024, month + 1, 15);
            transactions.Add(CreateTransaction(Guid.NewGuid(), userId, 100 + month * 10, "Food", date, categoryId));
            transactions.Add(CreateTransaction(Guid.NewGuid(), userId, 50 + month * 5, "Transport", date, categoryId));
            transactions.Add(CreateTransaction(Guid.NewGuid(), userId, 200 + month * 20, "Utilities", date, categoryId));
        }

        _mockTransactionRepository.Setup(x => x.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        var category = new Category("Food", CategoryType.Expense, userId, "🍔", "#FF6B6B", null, false, 0);
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(category, categoryId);
        
        _mockCategoryRepository.Setup(x => x.GetActiveByTypeAsync(userId, CategoryType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category> { category });

        _mockRecurringRepository.Setup(x => x.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecurringTransaction>());

        // Act
        var result = await _predictionService.PredictExpensesAsync(userId, 1);

        // Assert
        Assert.NotEqual(PredictionConfidence.InsufficientData, result.Confidence);
        Assert.True(result.MonthsAnalyzed >= 2);
        Assert.True(result.TotalTransactionsAnalyzed >= 10);
        Assert.NotEmpty(result.CategoryPredictions);
        Assert.True(result.TotalPredicted.Amount > 0);
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_ReturnsIncreasingTrend_WhenRecentHigher()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var transactions = new List<Transaction>();
        
        // Older months: low spending
        for (int month = 0; month < 3; month++)
        {
            var date = new DateTime(2023, month + 10, 15);
            transactions.Add(CreateTransaction(Guid.NewGuid(), userId, 100, "Food", date, categoryId));
        }
        
        // Recent months: higher spending
        for (int month = 0; month < 3; month++)
        {
            var date = new DateTime(2024, month + 1, 15);
            transactions.Add(CreateTransaction(Guid.NewGuid(), userId, 200, "Food", date, categoryId));
        }

        _mockTransactionRepository.Setup(x => x.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        var category = new Category("Food", CategoryType.Expense, userId, "🍔", "#FF6B6B", null, false, 0);
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(category, categoryId);
        
        _mockCategoryRepository.Setup(x => x.GetActiveByTypeAsync(userId, CategoryType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category> { category });

        // Act
        var result = await _predictionService.AnalyzeTrendsAsync(userId);

        // Assert
        Assert.NotEmpty(result);
        var foodTrend = result.FirstOrDefault(t => t.CategoryId.Value == categoryId);
        Assert.NotNull(foodTrend);
        Assert.Equal(SpendingTrend.Increasing, foodTrend.Trend);
    }

    [Fact]
    public async Task DetectAnomaliesAsync_ReturnsAnomalies_WhenTransactionExceedsThreshold()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var transactions = new List<Transaction>();
        
        // Normal transactions around 100
        for (int i = 0; i < 10; i++)
        {
            var date = DateTime.UtcNow.AddDays(-i * 3);
            transactions.Add(CreateTransaction(Guid.NewGuid(), userId, 100 + (i % 5) * 10, "Food", date, categoryId));
        }
        
        // Anomalous transaction
        transactions.Add(CreateTransaction(Guid.NewGuid(), userId, 500, "Food", DateTime.UtcNow.AddDays(-1), categoryId));

        _mockTransactionRepository.Setup(x => x.GetByTypeAndDateRangeAsync(
            userId, TransactionType.Expense, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        var category = new Category("Food", CategoryType.Expense, userId, "🍔", "#FF6B6B", null, false, 0);
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(category, categoryId);
        
        _mockCategoryRepository.Setup(x => x.GetByIdAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        // Act
        var result = await _predictionService.DetectAnomaliesAsync(userId);

        // Assert
        Assert.NotEmpty(result);
        var anomaly = result.FirstOrDefault(a => a.Amount.Amount == 500);
        Assert.NotNull(anomaly);
        Assert.True(anomaly.DeviationPercentage > 100);
    }

    private Transaction CreateTransaction(Guid id, Guid userId, decimal amount, string categoryName, DateTime date, Guid? categoryId = null)
    {
        var category = new Category(categoryName, CategoryType.Expense, userId, "🍔", "#FF6B6B", null, false, 0);
        if (categoryId.HasValue) 
            typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(category, categoryId.Value);
        
        var account = new Account("Test Account", AccountType.Cash, new Money(1000), userId);
        
        var transaction = new Transaction(
            TransactionType.Expense,
            new Money(amount),
            date,
            new AccountId(account.Id),
            new CategoryId(category.Id),
            userId);
        
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(transaction, id);
        return transaction;
    }
}