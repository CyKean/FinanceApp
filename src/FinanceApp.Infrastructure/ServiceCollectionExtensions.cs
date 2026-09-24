namespace FinanceApp.Infrastructure;

using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Repositories;
using FinanceApp.Infrastructure.Services;
using FinanceApp.Infrastructure.Supabase;
using FinanceApp.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        services.AddDbContext<FinanceAppDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseSqlite(dbOptions.SqliteConnectionString);

            if (dbOptions.EnableSensitiveDataLogging)
                options.EnableSensitiveDataLogging();
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();
        services.AddScoped<IFinancialGoalRepository, FinancialGoalRepository>();
        services.AddScoped<ISyncOperationRepository, SyncOperationRepository>();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IGuidGenerator, GuidGenerator>();
        services.AddSingleton<ICurrencyService, CurrencyService>();
        services.AddSingleton<IConnectivityService, ConnectivityService>();
        services.AddSingleton<INotificationService, NotificationService>();

        // Supabase services - Stage 3: Real implementation
        services.AddScoped<ISupabaseSyncService, SupabaseSyncService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        return services;
    }
}