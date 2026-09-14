namespace FinanceApp.Domain.Exceptions;

public abstract class DomainException : Exception
{
    public string ErrorCode { get; }

    protected DomainException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
}

public class ValidationException : DomainException
{
    public ValidationException(string message) : base(message, "VALIDATION_ERROR") { }
    public ValidationException(string message, string errorCode) : base(message, errorCode) { }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string entityName, Guid id) 
        : base($"{entityName} with id {id} was not found", "NOT_FOUND") { }

    public NotFoundException(string message) : base(message, "NOT_FOUND") { }
}

public class ConcurrencyException : DomainException
{
    public ConcurrencyException(string message) : base(message, "CONCURRENCY_ERROR") { }
}

public class InsufficientFundsException : DomainException
{
    public InsufficientFundsException(string message) : base(message, "INSUFFICIENT_FUNDS") { }
}

public class InvalidOperationDomainException : DomainException
{
    public InvalidOperationDomainException(string message) : base(message, "INVALID_OPERATION") { }
}

public class SyncConflictException : DomainException
{
    public Guid EntityId { get; }
    public int ServerVersion { get; }
    public int LocalVersion { get; }

    public SyncConflictException(Guid entityId, int serverVersion, int localVersion)
        : base($"Sync conflict for entity {entityId}: server version {serverVersion}, local version {localVersion}", "SYNC_CONFLICT")
    {
        EntityId = entityId;
        ServerVersion = serverVersion;
        LocalVersion = localVersion;
    }
}