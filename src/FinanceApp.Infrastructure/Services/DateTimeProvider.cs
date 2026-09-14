namespace FinanceApp.Infrastructure.Services;

using FinanceApp.Domain.Interfaces;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow => DateTime.Now;
}