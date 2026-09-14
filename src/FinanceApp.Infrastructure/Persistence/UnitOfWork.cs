namespace FinanceApp.Infrastructure.Persistence;

using FinanceApp.Domain.Interfaces;
using FinanceApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public class UnitOfWork : IUnitOfWork
{
    private readonly FinanceAppDbContext _context;
    private IDbContextTransaction? _transaction;

    public IAccountRepository Accounts { get; }
    public ICategoryRepository Categories { get; }
    public ITransactionRepository Transactions { get; }
    public IBudgetRepository Budgets { get; }
    public IRecurringTransactionRepository RecurringTransactions { get; }
    public IFinancialGoalRepository FinancialGoals { get; }
    public ISyncOperationRepository SyncOperations { get; }

    public UnitOfWork(
        FinanceAppDbContext context,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        IBudgetRepository budgetRepository,
        IRecurringTransactionRepository recurringTransactionRepository,
        IFinancialGoalRepository financialGoalRepository,
        ISyncOperationRepository syncOperationRepository)
    {
        _context = context;
        Accounts = accountRepository;
        Categories = categoryRepository;
        Transactions = transactionRepository;
        Budgets = budgetRepository;
        RecurringTransactions = recurringTransactionRepository;
        FinancialGoals = financialGoalRepository;
        SyncOperations = syncOperationRepository;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
            throw new InvalidOperationException("Transaction already started");

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            throw new InvalidOperationException("No transaction to commit");

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            throw new InvalidOperationException("No transaction to rollback");

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}