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

/// <summary>Income or expense for one calendar day. Produced by an aggregate, not a row.</summary>
public record DailyTotal(int Year, int Month, int Day, TransactionType Type, decimal Total);

/// <summary>
/// What one account's transactions add up to: income minus expense, in the
/// currency the transactions were recorded in. Added to an account's initial
/// balance this is the account's balance, which is why it is kept separate from
/// the stored figure and recomputed on demand.
/// </summary>
public record AccountNetAmount(Guid AccountId, decimal Amount, string Currency);