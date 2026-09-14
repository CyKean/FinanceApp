namespace FinanceApp.Domain.Interfaces;

using FinanceApp.Domain.ValueObjects;

public interface ICurrencyService
{
    string DefaultCurrency { get; }
    Money Convert(Money amount, string targetCurrency);
    Task<Money> ConvertAsync(Money amount, string targetCurrency, CancellationToken cancellationToken = default);
    IReadOnlyList<string> GetSupportedCurrencies();
}