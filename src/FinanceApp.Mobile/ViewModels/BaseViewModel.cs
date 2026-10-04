namespace FinanceApp.Mobile.ViewModels;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using FinanceApp.Application;
using Microsoft.Extensions.DependencyInjection;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// True only while the very first load is running. Pages bind their skeleton
    /// to this rather than to <see cref="IsBusy"/>, because blanking the page on
    /// every refresh is worse than showing stale data.
    /// </summary>
    [ObservableProperty]
    private bool _isInitialLoading;

    [ObservableProperty]
    private string _busyMessage = "Please wait...";

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>Set once a load has finished, successfully or not.</summary>
    private bool _hasLoadedOnce;

    /// <summary>The <see cref="SyncNotifications.Version"/> this view model's data came from.</summary>
    private int _loadedVersion = -1;

    private readonly IServiceScopeFactory? _scopeFactory;

    public BaseViewModel()
    {
    }

    /// <param name="scopeFactory">
    /// Required by <see cref="QueryOffUiThreadAsync"/>. Pass it to any view model
    /// that queries the database.
    /// </param>
    protected BaseViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Derived from <see cref="IsBusy"/> rather than set by each view model.
    /// Every LoadAsync in the app already brackets its work with IsBusy, so
    /// deriving here means a new page gets a skeleton by default instead of by
    /// remembering to opt in - and refreshing a page that already has data keeps
    /// that data on screen.
    /// </summary>
    partial void OnIsBusyChanged(bool value)
    {
        if (value)
        {
            IsInitialLoading = !_hasLoadedOnce;
            return;
        }

        // Marked on completion either way: a failed first load should show its
        // error row, not an endless placeholder.
        _hasLoadedOnce = true;
        IsInitialLoading = false;
    }

    /// <summary>
    /// True when this view model already holds data that is still current, so the
    /// load can be skipped entirely.
    /// <para>
    /// Shell keeps a page alive across tab switches, so without this every tab
    /// tap re-ran the page's full query set. The comparison is one integer
    /// against a counter that moves on every local write, so a page only reloads
    /// when something actually changed - which is also what keeps the reload
    /// correct after an edit on some other page.
    /// </para>
    /// </summary>
    protected bool CanSkipReload() => _loadedVersion >= 0 && _loadedVersion == SyncNotifications.Version;

    /// <summary>Records the data this view model is now displaying as current.</summary>
    protected void MarkLoaded() => _loadedVersion = SyncNotifications.Version;

    /// <summary>
    /// Forces the next load to run even if nothing appears to have changed. Used
    /// by pull-to-refresh, which is an explicit request to re-read.
    /// </summary>
    protected void InvalidateLoad() => _loadedVersion = -1;

    /// <summary>
    /// Runs a database read on a worker thread, against its own DI scope.
    /// <para>
    /// EF Core's SQLite provider has no asynchronous I/O path: every ToListAsync
    /// and SumAsync completes synchronously on the calling thread. So an "async"
    /// load awaited from a page still executes on the UI thread, which is why
    /// navigation felt laggy and why a skeleton could not animate - the thread
    /// was busy drawing it. Moving the read to the pool lets the page paint and
    /// the skeleton move while the query runs.
    /// </para>
    /// <para>
    /// The scope matters as much as the thread. MAUI resolves pages from the root
    /// provider and AddDbContext is Scoped, so injected services share one
    /// process-wide DbContext; using it from a worker while anything else touches
    /// it is what previously made the predictions page fail outright. Each query
    /// therefore gets a scope of its own.
    /// </para>
    /// </summary>
    protected async Task<T> QueryOffUiThreadAsync<T>(
        Func<IServiceProvider, Task<T>> query,
        CancellationToken cancellationToken = default)
    {
        if (_scopeFactory is null)
            throw new InvalidOperationException(
                $"{GetType().Name} queries the database but was constructed without an IServiceScopeFactory. " +
                "Pass one to the base constructor.");

        return await Task.Run(async () =>
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            return await query(scope.ServiceProvider);
        }, cancellationToken);
    }

    protected void SetError(string message)
    {
        ErrorMessage = message;
        HasError = !string.IsNullOrEmpty(message);
    }

    protected void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }
}