namespace FinanceApp.Mobile.ViewModels;

using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public partial class CalendarViewModel : BaseViewModel
{
    private readonly IDashboardService _dashboardService;
    private readonly IAuthenticationService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<CalendarViewModel> _logger;

    [ObservableProperty]
    private DateTime _selectedMonth = DateTime.Today;

    [ObservableProperty]
    private IReadOnlyList<CalendarEventDto> _events = Array.Empty<CalendarEventDto>();

    [ObservableProperty]
    private IReadOnlyList<CalendarEventDto> _selectedDayEvents = Array.Empty<CalendarEventDto>();

    [ObservableProperty]
    private DateTime? _selectedDate;

    [ObservableProperty]
    private int _currentMonthIndex;

    private readonly string[] _monthNames = 
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    public CalendarViewModel(
        IDashboardService dashboardService,
        IAuthenticationService authService,
        INavigationService navigationService,
        IDialogService dialogService,
        IServiceScopeFactory scopeFactory,
        ILogger<CalendarViewModel> logger)
        : base(scopeFactory)
    {
        _dashboardService = dashboardService;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
        Title = "Calendar";
        
        CurrentMonthIndex = DateTime.Today.Month - 1;
        SelectedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        if (CanSkipReload()) return;

        IsBusy = true;
        ClearError();

        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            if (!userId.HasValue) return;

            var startOfMonth = new DateTime(SelectedMonth.Year, SelectedMonth.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            Events = await QueryOffUiThreadAsync(services =>
                services.GetRequiredService<IDashboardService>().GetCalendarEventsAsync(
                    userId.Value, startOfMonth, endOfMonth, cancellationToken: default));

            MarkLoaded();

            // Preselect today (or keep the current selection) so the day list is never empty by surprise.
            var target = SelectedDate ?? DateTime.Today;
            if (target.Year == startOfMonth.Year && target.Month == startOfMonth.Month)
            {
                SelectedDate = target;
                SelectedDayEvents = Events.Where(e => e.Date.Date == target.Date).ToList();
            }
            else
            {
                SelectedDate = null;
                SelectedDayEvents = Array.Empty<CalendarEventDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading calendar events");
            SetError("Failed to load calendar");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        SelectedMonth = SelectedMonth.AddMonths(-1);
        CurrentMonthIndex = SelectedMonth.Month - 1;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        SelectedMonth = SelectedMonth.AddMonths(1);
        CurrentMonthIndex = SelectedMonth.Month - 1;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task CurrentMonthAsync()
    {
        SelectedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        CurrentMonthIndex = SelectedMonth.Month - 1;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SelectDateAsync(DateTime date)
    {
        SelectedDate = date;
        SelectedDayEvents = Events.Where(e => e.Date.Date == date.Date).ToList();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        InvalidateLoad();
        await LoadAsync();
    }

    partial void OnSelectedMonthChanged(DateTime value)
    {
        CurrentMonthIndex = value.Month - 1;
    }
}
