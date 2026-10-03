namespace FinanceApp.Infrastructure;

using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Repositories;
using FinanceApp.Infrastructure.Services;
using FinanceApp.Infrastructure.Supabase;
using FinanceApp.Infrastructure.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.IO;

public static class ServiceCollectionExtensions
{
    /// <param name="databaseDirectory">
    /// Platform-safe folder for the SQLite file (e.g. MAUI <c>FileSystem.AppDataDirectory</c>).
    /// Relative <c>Data Source</c> paths are resolved against it so the database
    /// always lands in the app sandbox instead of the process working directory.
    /// </param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string? databaseDirectory = null)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        services.AddHttpClient(OpenAiClient.HttpClientName)
            .ConfigureHttpClient((sp, client) =>
            {
                var aiOptions = sp.GetRequiredService<IOptions<AiOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(Math.Max(5, aiOptions.TimeoutSeconds));
            });
        services.AddSingleton<IAiClient, OpenAiClient>();

        services.AddDbContext<FinanceAppDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseSqlite(ResolveConnectionString(dbOptions.SqliteConnectionString, databaseDirectory));

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
        services.AddScoped<ILocalAccountStore, LocalAccountStore>();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IGuidGenerator, GuidGenerator>();
        services.AddSingleton<ICurrencyService, CurrencyService>();
        services.AddSingleton<IConnectivityService, ConnectivityService>();
        services.AddSingleton<INotificationService, NotificationService>();

        // Supabase services - Stage 3: Real implementation
        services.AddSingleton<SupabaseClientProvider>();
        services.AddScoped<ISupabaseSyncService, SupabaseSyncService>();
        services.AddScoped<ISyncService, SyncService>();
        // Offline-first auth: credentials are verified against local SQLite, so
        // register/sign-in never depend on Supabase being reachable.
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();

        return services;
    }

    internal static string ResolveConnectionString(string connectionString, string? databaseDirectory)
    {
        if (string.IsNullOrWhiteSpace(databaseDirectory))
            return connectionString;

        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (!Path.IsPathRooted(builder.DataSource))
            builder.DataSource = Path.Combine(databaseDirectory, builder.DataSource);

        return builder.ToString();
    }
}