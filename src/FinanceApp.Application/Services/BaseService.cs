namespace FinanceApp.Application.Services;

using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

public abstract class BaseService
{
    protected readonly IUnitOfWork UnitOfWork;
    protected readonly ILogger Logger;

    protected BaseService(IUnitOfWork unitOfWork, ILogger logger)
    {
        UnitOfWork = unitOfWork;
        Logger = logger;
    }

    protected async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        await UnitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action();
            await UnitOfWork.CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await UnitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    protected async Task ExecuteInTransactionAsync(
        Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        await UnitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await action();
            await UnitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await UnitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}