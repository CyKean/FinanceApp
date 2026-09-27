namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class CategoriesViewModel : BaseViewModel
{
    private readonly ICategoryService _categoryService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<CategoriesViewModel> _logger;

    [ObservableProperty]
    private IReadOnlyList<CategoryDto> _expenseCategories = Array.Empty<CategoryDto>();

    [ObservableProperty]
    private IReadOnlyList<CategoryDto> _incomeCategories = Array.Empty<CategoryDto>();

    [ObservableProperty]
    private CategoryType _selectedTab = CategoryType.Expense;

    public CategoriesViewModel(
        ICategoryService categoryService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<CategoriesViewModel> logger)
    {
        _categoryService = categoryService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
        Title = "Categories";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            ExpenseCategories = await _categoryService.GetActiveByTypeAsync(userId.Value, CategoryType.Expense);
            IncomeCategories = await _categoryService.GetActiveByTypeAsync(userId.Value, CategoryType.Income);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading categories");
            SetError("Failed to load categories");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        await _navigationService.NavigateToAsync($"//AddCategory?type={SelectedTab}");
    }

    [RelayCommand]
    private async Task EditCategoryAsync(CategoryDto category)
    {
        await _navigationService.NavigateToAsync($"//EditCategory?id={category.Id}");
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(CategoryDto category)
    {
        if (category.IsSystem)
        {
            await _dialogService.ShowAlertAsync("Cannot Delete", "System categories cannot be deleted");
            return;
        }

        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Delete Category",
            $"Are you sure you want to delete '{category.Name}'?",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            await _categoryService.DeleteAsync(category.Id, userId.Value);

            if (category.Type == CategoryType.Expense)
                ExpenseCategories = ExpenseCategories.Where(c => c.Id != category.Id).ToList();
            else
                IncomeCategories = IncomeCategories.Where(c => c.Id != category.Id).ToList();

            await _dialogService.ShowToastAsync("Category deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category");
            if (ex is FinanceApp.Domain.Exceptions.ValidationException vex) await _dialogService.ShowErrorToastAsync(vex.Message);
            else await _dialogService.ShowToastAsync("Delete failed. Please try again.");
        }
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(CategoryDto category)
    {
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            var updateDto = new UpdateCategoryDto(IsActive: !category.IsActive);
            await _categoryService.UpdateAsync(category.Id, updateDto, userId.Value);

            if (category.Type == CategoryType.Expense)
                ExpenseCategories = ExpenseCategories.Select(c => c.Id == category.Id ? c with { IsActive = !category.IsActive } : c).ToList();
            else
                IncomeCategories = IncomeCategories.Select(c => c.Id == category.Id ? c with { IsActive = !category.IsActive } : c).ToList();

            await _dialogService.ShowToastAsync(category.IsActive ? "Category deactivated" : "Category activated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling category");
            SetError("Failed to update category");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    partial void OnSelectedTabChanged(CategoryType value)
    {
        // Tab changed - UI will update automatically
    }

    [RelayCommand]
    private void SetTab(CategoryType tab)
    {
        SelectedTab = tab;
    }
}
