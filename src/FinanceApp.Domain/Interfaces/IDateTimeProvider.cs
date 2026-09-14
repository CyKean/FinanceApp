namespace FinanceApp.Domain.Interfaces;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateTime Today => UtcNow.Date;
    DateTime LocalNow { get; }
    DateTime LocalToday => LocalNow.Date;
}