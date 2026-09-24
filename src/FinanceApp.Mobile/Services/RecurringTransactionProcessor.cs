namespace FinanceApp.Mobile.Services;

using FinanceApp.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

public class RecurringTransactionProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecurringTransactionProcessor> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _initialDelay = TimeSpan.FromMinutes(1);

    public RecurringTransactionProcessor(IServiceProvider serviceProvider, ILogger<RecurringTransactionProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Recurring transaction processor started");

        // Initial delay to let app startup complete
        await Task.Delay(_initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueRecurringTransactionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing recurring transactions");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Recurring transaction processor stopped");
    }

    private async Task ProcessDueRecurringTransactionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var recurringService = scope.ServiceProvider.GetRequiredService<IRecurringTransactionService>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

        var userId = await authService.GetCurrentUserIdAsync(cancellationToken);
        if (!userId.HasValue) return;

        var now = DateTime.UtcNow;
        await recurringService.ProcessDueTransactionsAsync(userId.Value, now, cancellationToken);
        
        _logger.LogDebug("Processed due recurring transactions for user {UserId}", userId);
    }
}