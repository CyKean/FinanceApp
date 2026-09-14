namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class AddBudgetViewModel : BaseViewModel
{
    private readonly IBudgetService _budgetService;
    private readonly ICategoryService _categoryService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<AddBudgetViewModel> _logger;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Money _amount = Money.Zero();

    [ObservableProperty]
    private DateTime _startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private DateTime _endDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1);

    [ObservableProperty]
    private CategoryDto? _selectedCategory;

    [ObservableProperty]
    private IReadOnlyList<CategoryDto> _categories = Array.Empty<CategoryDto>();

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private Guid? _editingBudgetId;

    public AddBudgetViewModel(
        IBudgetService budgetService,
        ICategoryService categoryService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<AddBudgetViewModel> logger)
    {
        _budgetService = budgetService;
        _categoryService = categoryService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
    }

    public async Task InitializeAsync(Guid? budgetId = null)
    {
        IsEditing = budgetId.HasValue;
        EditingBudgetId = budgetId;
        Title = IsEditing ? "Edit Budget" : "Add Budget";

        await LoadCategoriesAsync();

        if (IsEditing && budgetId.HasValue)
        {
            await LoadBudgetAsync(budgetId.Value);
        }
    }

    private async Task LoadCategoriesAsync()
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue) return;

        Categories = await _categoryService.GetActiveByTypeAsync(userId.Value, CategoryType.Expense);
    }

    private async Task LoadBudgetAsync(Guid budgetId)
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue) return;

        var budget = await _budgetService.GetByIdAsync(budgetId, userId.Value);
        if (budget == null) return;

        Name = budget.Name;
        Amount = budget.Amount;
        StartDate = budget.StartDate;
        EndDate = budget.EndDate;
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == budget.CategoryId.Value);
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

            if (IsEditing && EditingBudgetId.HasValue)
            {
                var updateDto = new UpdateBudgetDto(
                    Name,
                    Amount,
                    StartDate,
                    EndDate,
                    SelectedCategory != null ? new CategoryId(SelectedCategory.Id) : null);

                await _budgetService.UpdateAsync(EditingBudgetId.Value, updateDto, userId.Value);
                await _dialogService.ShowToastAsync("Budget updated");
            }
            else
            {
                var createDto = new CreateBudgetDto(
                    Name,
                    Amount,
                    StartDate,
                    EndDate,
                    new CategoryId(SelectedCategory!.Id));

                await _budgetService.CreateAsync(createDto, userId.Value);
                await _dialogService.ShowToastAsync("Budget created");
            }

            await _navigationService.GoBackAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving budget");
            SetError("Failed to save budget");
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

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            SetError("Budget name is required");
            return false;
        }

        if (Amount.Amount <= 0)
        {
            SetError("Budget amount must be greater than zero");
            return false;
        }

        if (EndDate < StartDate)
        {
            SetError("End date must be after start date");
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