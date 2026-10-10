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

public class SyncServiceTests
{
    private readonly Mock<ISyncOperationRepository> _mockSyncRepository;
    private readonly Mock<ITransactionRepository> _mockTransactionRepository;
    private readonly Mock<IAccountRepository> _mockAccountRepository;
    private readonly Mock<ICategoryRepository> _mockCategoryRepository;
    private readonly Mock<IBudgetRepository> _mockBudgetRepository;
    private readonly Mock<IRecurringTransactionRepository> _mockRecurringRepository;
    private readonly Mock<IFinancialGoalRepository> _mockGoalRepository;
    private readonly Mock<ISupabaseSyncService> _mockSupabaseSyncService;
    private readonly Mock<IConnectivityService> _mockConnectivityService;
    private readonly Mock<ILogger<SyncService>> _mockLogger;
    private readonly SyncService _syncService;

    public SyncServiceTests()
    {
        _mockSyncRepository = new Mock<ISyncOperationRepository>();
        _mockTransactionRepository = new Mock<ITransactionRepository>();
        _mockAccountRepository = new Mock<IAccountRepository>();
        _mockCategoryRepository = new Mock<ICategoryRepository>();
        _mockBudgetRepository = new Mock<IBudgetRepository>();
        _mockRecurringRepository = new Mock<IRecurringTransactionRepository>();
        _mockGoalRepository = new Mock<IFinancialGoalRepository>();
        _mockSupabaseSyncService = new Mock<ISupabaseSyncService>();
        _mockConnectivityService = new Mock<IConnectivityService>();
        _mockConnectivityService.Setup(x => x.CheckConnectivityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _mockConnectivityService.Object.CurrentAccess);

        _mockAccountRepository.Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account>());
        _mockCategoryRepository.Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category>());
        _mockTransactionRepository.Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transaction>());
        _mockBudgetRepository.Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Budget>());
        _mockRecurringRepository.Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecurringTransaction>());
        _mockGoalRepository.Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FinancialGoal>());
        _mockSyncRepository.Setup(x => x.GetByEntityAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SyncOperation>());
        _mockSyncRepository.Setup(x => x.GetTrackedEntitiesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<(string, Guid)>());
        // The outbox heal now asks the database which rows have no operation
        // rather than diffing a full load of every entity table. An empty result
        // is the steady state for these tests: nothing needs queueing.
        _mockSyncRepository.Setup(x => x.GetUntrackedEntitiesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(string EntityType, Guid EntityId, FinanceApp.Domain.Enums.SyncStatus SyncStatus)>());
        _mockLogger = new Mock<ILogger<SyncService>>();

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _syncService = new SyncService(
            mockUnitOfWork.Object,
            _mockSyncRepository.Object,
            _mockTransactionRepository.Object,
            _mockAccountRepository.Object,
            _mockCategoryRepository.Object,
            _mockBudgetRepository.Object,
            _mockRecurringRepository.Object,
            _mockGoalRepository.Object,
            _mockSupabaseSyncService.Object,
            _mockConnectivityService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task SyncAsync_ReturnsNoInternet_WhenOffline()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockConnectivityService.Setup(x => x.CurrentAccess).Returns(NetworkAccess.None);

        // Act
        var result = await _syncService.SyncAsync(userId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No internet connection", result.ErrorMessage);
        Assert.Equal(0, result.SyncedCount);
    }

    [Fact]
    public async Task SyncAsync_ReturnsAlreadyInProgress_WhenSyncRunning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        _mockConnectivityService.Setup(x => x.CurrentAccess).Returns(NetworkAccess.Internet);
        
        // Setup mock to return a pending operation
        var pendingOp = new SyncOperation("Transaction", transactionId, SyncOperationType.Create, userId);
        _mockSyncRepository.Setup(x => x.GetPendingByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SyncOperation> { pendingOp });
        
        // Setup transaction repository to return the transaction
        var transaction = new Transaction(
            TransactionType.Expense,
            new Money(100),
            DateTime.Today,
            new AccountId(Guid.NewGuid()),
            new CategoryId(Guid.NewGuid()),
            userId);
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(transaction, transactionId);
        
        _mockTransactionRepository.Setup(x => x.GetByIdIncludingDeletedAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        
        // Use callback to signal when first sync has acquired lock and is processing
        var firstSyncProcessing = new TaskCompletionSource<bool>();
        var releaseFirstSync = new TaskCompletionSource<object>();
        
        _mockSupabaseSyncService.Setup(x => x.SyncTransactionAsync(
            It.IsAny<Transaction>(), 
            It.IsAny<SyncOperationType>(), 
            It.IsAny<CancellationToken>()))
            .Callback(() => firstSyncProcessing.SetResult(true))
            .Returns(() => releaseFirstSync.Task);

        // Start first sync - this should acquire the lock
        var firstSync = _syncService.SyncAsync(userId);
        
        // Wait for first sync to acquire lock and start processing (callback fired)
        await firstSyncProcessing.Task;

        // Verify first sync is running (lock is held)
        var isSyncing = await _syncService.IsSyncingAsync(userId);
        Assert.True(isSyncing);

        // Act - Try second sync while first is still running
        // This should return immediately with "already in progress" (WaitAsync(0))
        var secondResult = await _syncService.SyncAsync(userId);

        // Release first sync
        releaseFirstSync.SetResult(null);
        await firstSync;

        // Assert
        Assert.False(secondResult.Success);
        Assert.Equal("Sync already in progress", secondResult.ErrorMessage);
    }

    [Fact]
    public async Task SyncAsync_ReturnsSuccess_WhenNoPendingOperations()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockConnectivityService.Setup(x => x.CurrentAccess).Returns(NetworkAccess.Internet);
        _mockSyncRepository.Setup(x => x.GetPendingByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SyncOperation>());

        // Act
        var result = await _syncService.SyncAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.SyncedCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task SyncAsync_SyncsTransaction_WhenPendingCreate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        
        _mockConnectivityService.Setup(x => x.CurrentAccess).Returns(NetworkAccess.Internet);
        
        var transaction = new Transaction(
            TransactionType.Expense,
            new Money(100),
            DateTime.Today,
            new AccountId(Guid.NewGuid()),
            new CategoryId(Guid.NewGuid()),
            userId);
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(transaction, transactionId);

        _mockSyncRepository.SetupSequence(x => x.GetPendingByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SyncOperation> 
            { 
                new SyncOperation("Transaction", transactionId, SyncOperationType.Create, userId) 
            })
            .ReturnsAsync(new List<SyncOperation>()); // Second call after sync

        _mockTransactionRepository.Setup(x => x.GetByIdIncludingDeletedAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        _mockSupabaseSyncService.Setup(x => x.SyncTransactionAsync(
            It.IsAny<Transaction>(), 
            It.IsAny<SyncOperationType>(), 
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _syncService.SyncAsync(userId);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(1, result.SyncedCount);
        
        // Verify supabase was called
        _mockSupabaseSyncService.Verify(
            x => x.SyncTransactionAsync(
                It.Is<Transaction>(t => t.Id == transactionId), 
                SyncOperationType.Create, 
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncAsync_HandlesFailure_AndIncrementsRetry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        
        _mockConnectivityService.Setup(x => x.CurrentAccess).Returns(NetworkAccess.Internet);
        
        var transaction = new Transaction(
            TransactionType.Expense,
            new Money(100),
            DateTime.Today,
            new AccountId(Guid.NewGuid()),
            new CategoryId(Guid.NewGuid()),
            userId);
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(transaction, transactionId);

        var syncOp = new SyncOperation("Transaction", transactionId, SyncOperationType.Create, userId);
        
        _mockSyncRepository.SetupSequence(x => x.GetPendingByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SyncOperation> { syncOp })
            .ReturnsAsync(new List<SyncOperation>());

        _mockTransactionRepository.Setup(x => x.GetByIdIncludingDeletedAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        _mockSupabaseSyncService.Setup(x => x.SyncTransactionAsync(
            It.IsAny<Transaction>(), 
            It.IsAny<SyncOperationType>(), 
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network error"));

        // Act
        var result = await _syncService.SyncAsync(userId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, result.SyncedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains("Network error", result.ErrorMessage);
    }
}