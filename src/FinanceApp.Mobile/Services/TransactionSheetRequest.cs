namespace FinanceApp.Mobile.Services;

using FinanceApp.Domain.Enums;

/// <summary>
/// Hands the requested transaction mode to the bottom sheet deterministically.
/// Shell query parameters proved unreliable for reused pages, so the opener
/// stashes the mode here and the sheet consumes it on initialize.
/// </summary>
public class TransactionSheetRequest
{
    private TransactionType? _pending;

    public void Request(TransactionType type)
    {
        _pending = type;
    }

    public bool TryTake(out TransactionType type)
    {
        if (_pending.HasValue)
        {
            type = _pending.Value;
            _pending = null;
            return true;
        }

        type = default;
        return false;
    }
}
