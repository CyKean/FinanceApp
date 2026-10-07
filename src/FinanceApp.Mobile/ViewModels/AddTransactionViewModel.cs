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
    private readonly TransactionSheetRequest _sheetRequest;
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

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private bool _isSaveAvailable = true;

    private TransactionType _originalType;
    private decimal _originalAmount;
    private DateTime _originalDate;
    private string _originalNotes = string.Empty;
    private Guid? _originalAccountId;
    private Guid? _originalCategoryId;

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
        TransactionSheetRequest sheetRequest,
        ILogger<AddTransactionViewModel> logger)
    {
        _transactionService = transactionService;
        _categoryService = categoryService;
        _accountService = accountService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _sheetRequest = sheetRequest;
        _logger = logger;

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsBusy))
                RecomputeHasChanges();
        };
    }

    public async Task InitializeAsync(TransactionType type, Guid? transactionId = null)
    {
        // The explicit in-app request wins over the URI query, which Shell
        // does not reliably deliver to reused pages.
        if (_sheetRequest.TryTake(out var requested))
            type = requested;

        TransactionType = type;
        Title = type == TransactionType.Expense ? "Add Expense" : "Add Income";
        IsEditing = transactionId.HasValue;
        EditingTransactionId = transactionId;
        IsAddingAccount = false;
        IsAddingCategory = false;
        NewAccountName = string.Empty;
        NewCategoryName = string.Empty;

        if (IsEditing && transactionId.HasValue)
        {
            Title = type == TransactionType.Expense ? "Edit Expense" : "Edit Income";
        }

        // The selection lists must exist before the transaction's ids can be
        // resolved into AccountDto/CategoryDto rows.
        await LoadAccountsAndCategoriesAsync();

        if (IsEditing && transactionId.HasValue)
            await LoadTransactionAsync(transactionId.Value);

        SaveOriginalSnapshot();
        RecomputeHasChanges();
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
        AmountText = transaction.Amount.Amount.ToString(CultureInfo.InvariantCulture);
        Date = transaction.Date;
        Notes = transaction.Notes ?? string.Empty;
        TransactionType = transaction.Type;

        // The type change above kicks off an async category reload; fetch the
        // matching list synchronously so the selection resolves against it.
        var categoryType = transaction.Type == TransactionType.Expense ? CategoryType.Expense : CategoryType.Income;
        Categories = await _categoryService.GetActiveByTypeAsync(userId.Value, categoryType);

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
                if (!HasChanges)
                {
                    await _dialogService.ShowToastAsync("No changes to save");
                    return;
                }

                var updateDto = new UpdateTransactionDto(
                    Amount,
                    Date,
                    string.IsNullOrWhiteSpace(Notes) ? string.Empty : Notes.Trim(),
                    SelectedAccount != null ? new AccountId(SelectedAccount.Id) : null,
                    SelectedCategory != null ? new CategoryId(SelectedCategory.Id) : null,
                    TransactionType);

                await _transactionService.UpdateAsync(EditingTransactionId.Value, updateDto, userId.Value);
                await _dialogService.ShowSuccessAsync("Transaction updated");
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
                await _dialogService.ShowSuccessAsync("Transaction added");
            }

            if (OnSavedCallback != null)
                await OnSavedCallback();
            else
                await _navigationService.NavigateToAsync("//Transactions");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving transaction");
            await _dialogService.ShowFailureAsync("Failed to save transaction");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (IsEditing && HasChanges)
        {
            var discard = await _dialogService.ShowConfirmationAsync(
                "Unsaved changes",
                "You have unsaved changes. Are you sure you want to leave?",
                "Discard",
                "Stay");
            if (!discard) return;
        }

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
            await _dialogService.ShowSuccessAsync("Account added");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding account inline");
            await _dialogService.ShowFailureAsync("Failed to add account");
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
            await _dialogService.ShowSuccessAsync("Category added");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding category inline");
            await _dialogService.ShowFailureAsync("Failed to add category");
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
        RecomputeHasChanges();
        _ = LoadCategoriesAsync();
    }

    partial void OnAmountTextChanged(string value) => RecomputeHasChanges();
    partial void OnNotesChanged(string value) => RecomputeHasChanges();
    partial void OnDateChanged(DateTime value) => RecomputeHasChanges();
    partial void OnSelectedAccountChanged(AccountDto? value) => RecomputeHasChanges();
    partial void OnSelectedCategoryChanged(CategoryDto? value) => RecomputeHasChanges();

    private void SaveOriginalSnapshot()
    {
        _originalType = TransactionType;
        _originalAmount = decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
        _originalDate = Date;
        _originalNotes = Notes?.Trim() ?? string.Empty;
        _originalAccountId = SelectedAccount?.Id;
        _originalCategoryId = SelectedCategory?.Id;
    }

    private void RecomputeHasChanges()
    {
        if (!IsEditing)
        {
            HasChanges = true;
            IsSaveAvailable = !IsBusy;
            return;
        }

        var parsedAmount = decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : (decimal?)null;
        HasChanges =
            parsedAmount != _originalAmount ||
            TransactionType != _originalType ||
            Date.Date != _originalDate.Date ||
            !string.Equals(Notes?.Trim() ?? string.Empty, _originalNotes, StringComparison.Ordinal) ||
            SelectedAccount?.Id != _originalAccountId ||
            SelectedCategory?.Id != _originalCategoryId;

        IsSaveAvailable = !IsBusy && HasChanges;
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

        if (parsedAmount != decimal.Round(parsedAmount, 2))
        {
            SetError("Amount can have at most two decimal places");
            return false;
        }

        if (parsedAmount > 99_999_999m)
        {
            SetError("Amount is too large");
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