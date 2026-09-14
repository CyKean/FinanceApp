namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class AddTransactionViewModel : BaseViewModel
{
    private readonly ITransactionService _transactionService;
    private readonly ICategoryService _categoryService;
    private readonly IAccountService _accountService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<AddTransactionViewModel> _logger;

    [ObservableProperty]
    private TransactionType _transactionType = TransactionType.Expense;

    [ObservableProperty]
    private Money _amount = Money.Zero();

    [ObservableProperty]
    private DateTime _date = DateTime.Today;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private AccountDto? _selectedAccount;

    [ObservableProperty]
    private CategoryDto? _selectedCategory;

    [ObservableProperty]
    private IReadOnlyList<AccountDto> _accounts = Array.Empty<AccountDto>();

    [ObservableProperty]
    private IReadOnlyList<CategoryDto> _categories = Array.Empty<CategoryDto>();

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private Guid? _editingTransactionId;

    public AddTransactionViewModel(
        ITransactionService transactionService,
        ICategoryService categoryService,
        IAccountService accountService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<AddTransactionViewModel> logger)
    {
        _transactionService = transactionService;
        _categoryService = categoryService;
        _accountService = accountService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
    }

    public async Task InitializeAsync(TransactionType type, Guid? transactionId = null)
    {
        TransactionType = type;
        Title = type == TransactionType.Expense ? "Add Expense" : "Add Income";
        IsEditing = transactionId.HasValue;
        EditingTransactionId = transactionId;

        if (IsEditing && transactionId.HasValue)
        {
            Title = type == TransactionType.Expense ? "Edit Expense" : "Edit Income";
            await LoadTransactionAsync(transactionId.Value);
        }

        await LoadAccountsAndCategoriesAsync();
    }

    private async Task LoadAccountsAndCategoriesAsync()
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue) return;

        Accounts = await _accountService.GetAllAsync(userId.Value);

        if (SelectedAccount == null && Accounts.Any())
        {
            var defaultAccount = await _accountService.GetDefaultAsync(userId.Value);
            SelectedAccount = defaultAccount ?? Accounts.FirstOrDefault();
        }

        var categoryType = TransactionType == TransactionType.Expense ? CategoryType.Expense : CategoryType.Income;
        Categories = await _categoryService.GetActiveByTypeAsync(userId.Value, categoryType);
    }

    private async Task LoadTransactionAsync(Guid transactionId)
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue) return;

        var transaction = await _transactionService.GetByIdAsync(transactionId, userId.Value);
        if (transaction == null) return;

        Amount = transaction.Amount;
        Date = transaction.Date;
        Notes = transaction.Notes ?? string.Empty;

        SelectedAccount = Accounts.FirstOrDefault(a => a.Id == transaction.AccountId.Value);
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == transaction.CategoryId.Value);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (!ValidateInput())
            return;

        IsBusy = true;
        ClearError();

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            if (IsEditing && EditingTransactionId.HasValue)
            {
                var updateDto = new UpdateTransactionDto(
                    Amount,
                    Date,
                    string.IsNullOrWhiteSpace(Notes) ? null : Notes,
                    SelectedAccount != null ? new AccountId(SelectedAccount.Id) : null,
                    SelectedCategory != null ? new CategoryId(SelectedCategory.Id) : null);

                await _transactionService.UpdateAsync(EditingTransactionId.Value, updateDto, userId.Value);
                await _dialogService.ShowToastAsync("Transaction updated");
            }
            else
            {
                var createDto = new CreateTransactionDto(
                    TransactionType,
                    Amount,
                    Date,
                    new AccountId(SelectedAccount!.Id),
                    new CategoryId(SelectedCategory!.Id),
                    string.IsNullOrWhiteSpace(Notes) ? null : Notes);

                await _transactionService.CreateAsync(createDto, userId.Value);
                await _dialogService.ShowToastAsync("Transaction added");
            }

            await _navigationService.GoBackAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving transaction");
            SetError("Failed to save transaction");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await _navigationService.GoBackAsync();
    }

    [RelayCommand]
    private void SetExpenseType()
    {
        TransactionType = TransactionType.Expense;
    }

    [RelayCommand]
    private void SetIncomeType()
    {
        TransactionType = TransactionType.Income;
    }

    partial void OnTransactionTypeChanged(TransactionType value)
    {
        Title = value == TransactionType.Expense ? (IsEditing ? "Edit Expense" : "Add Expense") : (IsEditing ? "Edit Income" : "Add Income");
        _ = LoadCategoriesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue) return;

        var categoryType = TransactionType == TransactionType.Expense ? CategoryType.Expense : CategoryType.Income;
        Categories = await _categoryService.GetActiveByTypeAsync(userId.Value, categoryType);
    }

    private bool ValidateInput()
    {
        if (Amount.Amount <= 0)
        {
            SetError("Amount must be greater than zero");
            return false;
        }

        if (SelectedAccount == null)
        {
            SetError("Please select an account");
            return false;
        }

        if (SelectedCategory == null)
        {
            SetError("Please select a category");
            return false;
        }

        return true;
    }
}