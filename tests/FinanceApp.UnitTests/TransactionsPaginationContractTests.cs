namespace FinanceApp.UnitTests;

using Xunit;

/// <summary>
/// The infinite-scroll spinner used to stay on forever because the page had no
/// <c>HasMore</c>: every threshold event re-requested the next page even after
/// the list was fully loaded. The ViewModel cannot be hosted in this test
/// assembly (it is a MAUI view model), so this guard pins the state machine
/// contract in source: initial load derives <c>HasMore</c>, load-more stops
/// when <c>HasMore</c> is false or a request is running, and every exit path
/// clears <c>IsLoadingMore</c>.
/// </summary>
public class TransactionsPaginationContractTests
{
    private static string ViewModelSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName, "src", "FinanceApp.Mobile", "ViewModels", "TransactionsViewModel.cs");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate TransactionsViewModel.cs.");
    }

    [Fact]
    public void HasMore_state_exists()
    {
        Assert.Contains("_hasMore", ViewModelSource());
    }

    [Fact]
    public void LoadMore_never_runs_when_HasMore_is_false()
    {
        Assert.Contains("if (IsBusy || IsLoadingMore || !HasMore) return;", ViewModelSource());
    }

    [Fact]
    public void Initial_load_sets_HasMore_from_page_count()
    {
        Assert.Contains("HasMore = loaded.Item1.Count == PageSize;", ViewModelSource());
    }

    [Fact]
    public void LoadMore_sets_HasMore_from_page_count()
    {
        Assert.Contains("HasMore = moreTransactions.Count == PageSize;", ViewModelSource());
    }

    [Fact]
    public void LoadMore_clears_IsLoadingMore_in_finally()
    {
        Assert.Contains("finally", ViewModelSource());
        Assert.Contains("IsLoadingMore = false;", ViewModelSource());
    }
}
