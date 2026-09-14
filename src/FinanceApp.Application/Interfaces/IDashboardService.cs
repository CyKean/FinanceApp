namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.DTOs;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AnalyticsDto> GetAnalyticsAsync(Guid userId, int months, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CalendarEventDto>> GetCalendarEventsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}