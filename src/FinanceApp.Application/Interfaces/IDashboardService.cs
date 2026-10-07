namespace FinanceApp.Application.Interfaces;

using FinanceApp.Application.DTOs;
using FinanceApp.Domain.Enums;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AnalyticsDto> GetAnalyticsAsync(Guid userId, int months, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CalendarEventDto>> GetCalendarEventsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<StatisticsDto> GetStatisticsAsync(Guid userId, StatisticsPeriod period, CancellationToken cancellationToken = default);
}