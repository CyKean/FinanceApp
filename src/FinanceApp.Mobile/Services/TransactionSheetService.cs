namespace FinanceApp.Mobile.Services;

using FinanceApp.Domain.Enums;

/// <summary>
/// Implemented by the page-level host that renders the add-transaction sheet.
/// </summary>
public interface ITransactionSheetHost
{
    void HandleShow(TransactionType type);
}

/// <summary>
/// Asks the topmost <c>TransactionSheetHost</c> to show the add-transaction
/// form over the current page.
/// <para>
/// This replaces pushing the "AddTransactionSheet" Shell route: a ShellContent
/// route replaces the page behind it, so the dashboard could never stay visible
/// under the scrim. Hosting the sheet as an overlay keeps the page underneath.
/// </para>
/// <para>
/// Dispatch is routed through a single weak owner reference rather than an event
/// so that only the host on the visible page reacts — otherwise every page
/// hosting a sheet would open one.
/// </para>
/// </summary>
public sealed class TransactionSheetService
{
    private WeakReference<ITransactionSheetHost>? _owner;

    /// <summary>Raised after a transaction is saved, so pages can refresh.</summary>
    public event Action? Saved;

    /// <summary>Called by each host as it loads; the most recent host wins.</summary>
    public void Register(ITransactionSheetHost host) => _owner = new WeakReference<ITransactionSheetHost>(host);

    public void Unregister(ITransactionSheetHost host)
    {
        if (_owner is not null && _owner.TryGetTarget(out var current) && ReferenceEquals(current, host))
            _owner = null;
    }

    public void Show(TransactionType type)
    {
        if (_owner is not null && _owner.TryGetTarget(out var host))
            host.HandleShow(type);
    }

    public void NotifySaved() => Saved?.Invoke();
}
