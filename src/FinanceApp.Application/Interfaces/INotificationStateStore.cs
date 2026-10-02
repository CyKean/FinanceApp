namespace FinanceApp.Application.Interfaces;

/// <summary>
/// Persistence for notification read/dismissed state. Implemented per platform
/// so Application never touches platform storage APIs directly.
/// </summary>
public interface INotificationStateStore
{
    Task<HashSet<string>> LoadDismissedAsync(CancellationToken cancellationToken = default);

    Task<HashSet<string>> LoadReadAsync(CancellationToken cancellationToken = default);

    Task<HashSet<string>> LoadSurfacedAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyCollection<string> dismissed,
        IReadOnlyCollection<string> read,
        IReadOnlyCollection<string> surfaced,
        CancellationToken cancellationToken = default);
}