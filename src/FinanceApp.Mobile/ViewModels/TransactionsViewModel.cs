namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public partial class TransactionsViewModel : BaseViewModel
{
    private readonly ITransactionService _transactionService;
    private readonly ICategoryService _categoryService;
    private readonly IAccountService _accountService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly TransactionSheetRequest _sheetRequest;
    private readonly TransactionSheetService _transactionSheetService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<TransactionsViewModel> _logger;

    [ObservableProperty]
    private IReadOnlyList<TransactionDto> _transactions = Array.Empty<TransactionDto>();

    [ObservableProperty]
    private TransactionFilterDto _filter = new();

    [ObservableProperty]
    private TransactionSummaryDto? _summary;

    [ObservableProperty]
    private bool _isLoadingMore;

    /// <summary>False keeps the filter panel collapsed to a single summary row.</summary>
    [ObservableProperty]
    private bool _isFilterExpanded;

    public Func<object, Task>? AnimateDeleteAsync { get; set; }

    private int _currentPage = 1;
    private const int PageSize = 20;

    public TransactionsViewModel(
        ITransactionService transactionService,
        ICategoryService categoryService,
        IAccountService accountService,
        IAuthenticationService authService,
          INavigationService navigationService,
          TransactionSheetRequest sheetRequest,
          TransactionSheetService transactionSheetService,
          IDialogService dialogService,
          IServiceScopeFactory scopeFactory,
          ILogger<TransactionsViewModel> logger)
        : base(scopeFactory)
    {
        _transactionService = transactionService;
        _categoryService = categoryService;
        _accountService = accountService;
        _authService = authService;
          _navigationService = navigationService;
          _sheetRequest = sheetRequest;
          _transactionSheetService = transactionSheetService;
          _dialogService = dialogService;
        _logger = logger;
        Title = "Transactions";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        // Shell keeps this page alive, so coming back here should be free unless
        // something was actually written.
        if (CanSkipReload()) return;

        IsBusy = true;
        ClearError();
        _currentPage = 1;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            Filter = Filter with { Page = 1, PageSize = PageSize };

            var start = Filter.StartDate ?? DateTime.Today.AddMonths(-1);
            var end = Filter.EndDate ?? DateTime.Today;
            var filter = Filter;

            var loaded = await QueryOffUiThreadAsync(async services =>
            {
                var transactionService = services.GetRequiredService<ITransactionService>();

                return (
                    await transactionService.GetAllAsync(userId.Value, filter),
                    await transactionService.GetSummaryAsync(userId.Value, start, end));
            });

            Transactions = loaded.Item1;
            Summary = loaded.Item2;

            MarkLoaded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading transactions");
            SetError("Failed to load transactions");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || IsLoadingMore) return;

        IsLoadingMore = true;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            _currentPage++;
            Filter = Filter with { Page = _currentPage };
            var filter = Filter;
            var page = _currentPage;

            var moreTransactions = await QueryOffUiThreadAsync(services =>
                services.GetRequiredService<ITransactionService>().GetAllAsync(userId.Value, filter));

            Transactions = Transactions.Concat(moreTransactions).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading more transactions");
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private Task AddExpenseAsync() => OpenSheetAsync(TransactionType.Expense);

    [RelayCommand]
    private Task AddIncomeAsync() => OpenSheetAsync(TransactionType.Income);

    [RelayCommand]
    private async Task AddTransactionAsync()
    {
        var choice = await _dialogService.ShowChoiceSheetAsync("Add Transaction", "Expense", "Income");
        if (choice == "Expense")
            _transactionSheetService.Show(TransactionType.Expense);
        else if (choice == "Income")
            _transactionSheetService.Show(TransactionType.Income);
    }

    /// <summary>
    /// Shows the form as an overlay on this page instead of pushing the
    /// AddTransactionSheet route, so the list stays visible behind it.
    /// </summary>
    private Task OpenSheetAsync(TransactionType type)
    {
        _transactionSheetService.Show(type);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task EditTransactionAsync(TransactionDto transaction)
    {
        await _navigationService.NavigateToAsync($"//EditTransaction?id={transaction.Id}&type={transaction.Type}");
    }

    [RelayCommand]
    private async Task DeleteTransactionAsync(TransactionDto transaction)
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Delete Transaction",
            $"Are you sure you want to delete this {transaction.Type.ToString().ToLower()}?",
            "Delete",
            "Cancel",
            destructive: true);

        if (!confirmed) return;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            await _transactionService.DeleteAsync(transaction.Id, userId.Value);

            if (AnimateDeleteAsync is not null)
            {
                try
                {
                    await AnimateDeleteAsync(transaction);
                }
                catch (Exception animEx)
                {
                    _logger.LogWarning(animEx, "Delete animation failed for transaction {TransactionId}", transaction.Id);
                }
            }

            Transactions = Transactions.Where(t => t.Id != transaction.Id).ToList();
            await _dialogService.ShowToastAsync("Transaction deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting transaction");
            await _dialogService.ShowFailureAsync("Delete failed. Please try again.");
        }
    }

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        IsFilterExpanded = false;
        // A new filter asks for different rows, so the still-current check does
        // not apply even though nothing was written.
        InvalidateLoad();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        Filter = new TransactionFilterDto();
        IsFilterExpanded = false;
        InvalidateLoad();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        InvalidateLoad();
        await LoadAsync();
    }
}
