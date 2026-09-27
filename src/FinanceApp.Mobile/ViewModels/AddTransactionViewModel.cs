namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Globalization;

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

    /// <summary>
    /// Text-bound amount input. (Money.Amount is read-only so the Entry
    /// cannot bind to it directly; validated + parsed into <see cref="Amount"/>.)
    /// </summary>
    [ObservableProperty]
    private string _amountText = string.Empty;

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

    [ObservableProperty]
    private bool _isAddingAccount;

    [ObservableProperty]
    private string _newAccountName = string.Empty;

    [ObservableProperty]
    private AccountType _newAccountType = AccountType.Cash;

    [ObservableProperty]
    private bool _isAddingCategory;

    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    public IReadOnlyList<AccountType> AccountTypes { get; } =
        Enum.GetValues<AccountType>();

    /// <summary>
    /// Optional override invoked after a successful save instead of navigating back.
    /// Used by bottom-sheet hosts, which close themselves.
    /// </summary>
    public Func<Task>? OnSavedCallback { get; set; }

    /// <summary>
    /// Optional override invoked on cancel instead of navigating back.
    /// </summary>
    public Func<Task>? OnCancelledCallback { get; set; }

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

            if (OnSavedCallback != null)
                await OnSavedCallback();
            else
                await _navigationService.NavigateToAsync("//Transactions");
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
        if (OnCancelledCallback != null)
            await OnCancelledCallback();
        else
            await _navigationService.NavigateToAsync("//Transactions");
    }

    [RelayCommand]
    private void ShowAddAccount()
    {
        NewAccountName = string.Empty;
        IsAddingAccount = true;
    }

    [RelayCommand]
    private void CancelAddAccount()
    {
        IsAddingAccount = false;
    }

    [RelayCommand]
    private async Task SaveNewAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAccountName))
        {
            SetError("Account name is required");
            return;
        }

        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            var created = await _accountService.CreateAsync(
                new CreateAccountDto(NewAccountName.Trim(), NewAccountType, Money.Zero()),
                userId.Value);

            Accounts = await _accountService.GetAllAsync(userId.Value);
            SelectedAccount = Accounts.FirstOrDefault(a => a.Id == created.Id);
            IsAddingAccount = false;
            NewAccountName = string.Empty;
            await _dialogService.ShowToastAsync("Account added");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding account inline");
            SetError("Failed to add account");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ShowAddCategory()
    {
        NewCategoryName = string.Empty;
        IsAddingCategory = true;
    }

    [RelayCommand]
    private void CancelAddCategory()
    {
        IsAddingCategory = false;
    }

    [RelayCommand]
    private async Task SaveNewCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            SetError("Category name is required");
            return;
        }

        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            var categoryType = TransactionType == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
            var created = await _categoryService.CreateAsync(
                new CreateCategoryDto(NewCategoryName.Trim(), categoryType),
                userId.Value);

            Categories = await _categoryService.GetActiveByTypeAsync(userId.Value, categoryType);
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == created.Id);
            IsAddingCategory = false;
            NewCategoryName = string.Empty;
            await _dialogService.ShowToastAsync("Category added");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding category inline");
            SetError("Failed to add category");
        }
        finally
        {
            IsBusy = false;
        }
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
        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount) || parsedAmount <= 0)
        {
            SetError("Amount must be greater than zero");
            return false;
        }

        Amount = new Money(parsedAmount, Amount?.Currency ?? "PHP");

        if (SelectedAccount == null || SelectedAccount.Id == Guid.Empty)
        {
            SetError("Please select an account");
            return false;
        }

        if (SelectedCategory == null || SelectedCategory.Id == Guid.Empty)
        {
            SetError("Please select a category");
            return false;
        }

        return true;
    }
}