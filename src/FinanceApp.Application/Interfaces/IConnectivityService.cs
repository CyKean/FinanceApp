namespace FinanceApp.Application.Interfaces;

public interface IConnectivityService
{
    NetworkAccess CurrentAccess { get; }
    event Action<ConnectivityChangedEventArgs>? ConnectivityChanged;
    Task<NetworkAccess> CheckConnectivityAsync(CancellationToken cancellationToken = default);
}

public record ConnectivityChangedEventArgs(
    NetworkAccess PreviousAccess,
    NetworkAccess CurrentAccess
);

public enum NetworkAccess
{
    None = 0,
    Local = 1,
    Internet = 2
}