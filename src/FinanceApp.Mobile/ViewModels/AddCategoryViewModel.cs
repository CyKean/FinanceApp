namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

public partial class AddCategoryViewModel : BaseViewModel
{
    private readonly ICategoryService _categoryService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<AddCategoryViewModel> _logger;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private CategoryType _type = CategoryType.Expense;

    [ObservableProperty]
    private string _icon = string.Empty;

    [ObservableProperty]
    private string _color = "#512BD4";

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private Guid? _editingCategoryId;

    public AddCategoryViewModel(
        ICategoryService categoryService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<AddCategoryViewModel> logger)
    {
        _categoryService = categoryService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
    }

    public async Task InitializeAsync(CategoryType type, Guid? categoryId = null)
    {
        Type = type;
        IsEditing = categoryId.HasValue;
        EditingCategoryId = categoryId;
        Title = IsEditing ? "Edit Category" : "Add Category";

        if (IsEditing && categoryId.HasValue)
        {
            await LoadCategoryAsync(categoryId.Value);
        }
    }

    private async Task LoadCategoryAsync(Guid categoryId)
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        if (!userId.HasValue) return;

        var category = await _categoryService.GetByIdAsync(categoryId, userId.Value);
        if (category == null) return;

        Name = category.Name;
        Icon = category.Icon ?? string.Empty;
        Color = category.Color ?? "#512BD4";
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

            if (IsEditing && EditingCategoryId.HasValue)
            {
                var updateDto = new UpdateCategoryDto(Name, Icon, Color, null, null, null);
                await _categoryService.UpdateAsync(EditingCategoryId.Value, updateDto, userId.Value);
                await _dialogService.ShowToastAsync("Category updated");
            }
            else
            {
                var createDto = new CreateCategoryDto(Name, Type, Icon, Color);
                await _categoryService.CreateAsync(createDto, userId.Value);
                await _dialogService.ShowToastAsync("Category added");
            }

            await _navigationService.GoBackAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving category");
            SetError("Failed to save category");
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
            SetError("Category name is required");
            return false;
        }

        return true;
    }
}