namespace FinanceApp.Mobile.ViewModels;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

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

    public BaseViewModel()
    {
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