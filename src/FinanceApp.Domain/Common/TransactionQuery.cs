namespace FinanceApp.Domain.Common;

using FinanceApp.Domain.Enums;

/// <summary>
/// Filter for a transaction listing. Kept in the domain so the repository can
/// build one SQL statement (with LIMIT/OFFSET) instead of the service loading
/// every row and slicing in memory.
/// </summary>
public sealed record TransactionQuery
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public TransactionType? Type { get; init; }
    public Guid? AccountId { get; init; }
    public Guid? CategoryId { get; init; }

    public bool IsUnfiltered => !StartDate.HasValue && !EndDate.HasValue && !Type.HasValue
                               && !AccountId.HasValue && !CategoryId.HasValue;
}

/// <summary>Spend for one category over a window. Produced by an aggregate, not a row.</summary>
public record CategoryTotal(Guid CategoryId, decimal Total);

/// <summary>Income or expense for one calendar month. Produced by an aggregate, not a row.</summary>
public record MonthlyTotal(int Year, int Month, TransactionType Type, decimal Total);