namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class TransactionsViewModel : BaseViewModel
{
    private readonly ITransactionService _transactionService;
    private readonly ICategoryService _categoryService;
    private readonly IAccountService _accountService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly TransactionSheetRequest _sheetRequest;
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

    private int _currentPage = 1;
    private const int PageSize = 20;

    public TransactionsViewModel(
        ITransactionService transactionService,
        ICategoryService categoryService,
        IAccountService accountService,
        IAuthenticationService authService,
          INavigationService navigationService,
          TransactionSheetRequest sheetRequest,
          IDialogService dialogService,
          ILogger<TransactionsViewModel> logger)
    {
        _transactionService = transactionService;
        _categoryService = categoryService;
        _accountService = accountService;
        _authService = authService;
          _navigationService = navigationService;
          _sheetRequest = sheetRequest;
          _dialogService = dialogService;
        _logger = logger;
        Title = "Transactions";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();
        _currentPage = 1;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            Filter = Filter with { Page = 1, PageSize = PageSize };
            Transactions = await _transactionService.GetAllAsync(userId.Value, Filter);
            Summary = await _transactionService.GetSummaryAsync(userId.Value, Filter.StartDate ?? DateTime.Today.AddMonths(-1), Filter.EndDate ?? DateTime.Today);
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
            var moreTransactions = await _transactionService.GetAllAsync(userId.Value, Filter);
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
    private async Task AddExpenseAsync()
    {
        await _navigationService.NavigateToAsync("//AddTransactionSheet?type=Expense");
    }

    [RelayCommand]
    private async Task AddIncomeAsync()
    {
        await _navigationService.NavigateToAsync("//AddTransactionSheet?type=Income");
    }

    [RelayCommand]
    private async Task AddTransactionAsync()
    {
          var choice = await _dialogService.ShowActionSheetAsync("Add Transaction", "Cancel", null, "Expense", "Income");
          if (choice == "Expense")
          {
              _sheetRequest.Request(TransactionType.Expense);
              await _navigationService.NavigateToAsync("//AddTransactionSheet?type=Expense");
          }
          else if (choice == "Income")
          {
              _sheetRequest.Request(TransactionType.Income);
              await _navigationService.NavigateToAsync("//AddTransactionSheet?type=Income");
          }
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
            "Cancel");

        if (!confirmed) return;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            await _transactionService.DeleteAsync(transaction.Id, userId.Value);
            Transactions = Transactions.Where(t => t.Id != transaction.Id).ToList();
            await _dialogService.ShowToastAsync("Transaction deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting transaction");
            SetError("Failed to delete transaction");
        }
    }

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        Filter = new TransactionFilterDto();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }
}