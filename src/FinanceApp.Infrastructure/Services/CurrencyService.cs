namespace FinanceApp.Infrastructure.Services;

using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;

public class CurrencyService : ICurrencyService
{
    public string DefaultCurrency => "PHP";

    private static readonly IReadOnlyDictionary<string, decimal> ExchangeRates = new Dictionary<string, decimal>
    {
        { "PHP", 1.0m },
        { "USD", 0.018m },
        { "EUR", 0.016m },
        { "JPY", 2.7m }
    }.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    public Money Convert(Money amount, string targetCurrency)
    {
        if (amount.Currency == targetCurrency)
            return amount;

        if (!ExchangeRates.TryGetValue(amount.Currency, out var fromRate) ||
            !ExchangeRates.TryGetValue(targetCurrency, out var toRate))
        {
            throw new InvalidOperationException($"Unsupported currency conversion: {amount.Currency} to {targetCurrency}");
        }

        var usdAmount = amount.Amount * fromRate;
        var targetAmount = usdAmount / toRate;

        return new Money(Math.Round(targetAmount, 2), targetCurrency);
    }

    public Task<Money> ConvertAsync(Money amount, string targetCurrency, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Convert(amount, targetCurrency));
    }

    public IReadOnlyList<string> GetSupportedCurrencies()
    {
        return ExchangeRates.Keys.ToList();
    }
}