namespace FinanceApp.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class DatabaseInitializer
{
    private static bool s_extraColumnsEnsured;
    private static readonly object s_extraColumnsLock = new();

    public static async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FinanceAppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<FinanceAppDbContext>>();

        try
        {
            logger.LogInformation("Ensuring database is created...");
            await context.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation("Database initialized successfully");

            // Ground truth for "where did my data go" questions: path + row counts.
            logger.LogInformation(
                "Local database at {Path}: Accounts={Accounts}, Categories={Categories}, Transactions={Transactions}, Budgets={Budgets}, Goals={Goals}, PendingSyncOps={PendingOps}",
                context.Database.GetDbConnection().DataSource,
                await context.Accounts.CountAsync(cancellationToken),
                await context.Categories.CountAsync(cancellationToken),
                await context.Transactions.CountAsync(cancellationToken),
                await context.Budgets.CountAsync(cancellationToken),
                await context.FinancialGoals.CountAsync(cancellationToken),
                await context.SyncOperations.CountAsync(o => o.Status != FinanceApp.Domain.Enums.SyncStatus.Synced, cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing database");
            throw;
        }
    }

    /// <summary>
    /// Adds columns introduced after v1 to databases created by older builds.
    /// EnsureCreated only creates missing TABLES, never missing columns.
    /// Safe to call on every DbContext construction - runs once per process.
    /// </summary>
    public static void EnsureExtraColumns(FinanceAppDbContext context)
    {
        lock (s_extraColumnsLock)
        {
            if (s_extraColumnsEnsured)
                return;
            s_extraColumnsEnsured = true;
        }

        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        try
        {
            if (wasClosed)
                connection.Open();

            EnsureColumn(connection, "Budgets", "Icon", "TEXT");
            EnsureColumn(connection, "Budgets", "Color", "TEXT");
        }
        finally
        {
            if (wasClosed)
                connection.Close();
        }
    }

    private static void EnsureColumn(System.Data.Common.DbConnection connection, string table, string column, string type)
    {
        using var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info(\"{table}\")";
        using var reader = pragma.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return;
        }
        reader.Close();

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}";
        alter.ExecuteNonQuery();
    }

    public static async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FinanceAppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<FinanceAppDbContext>>();

        try
        {
            logger.LogInformation("Applying migrations...");
            await context.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Migrations applied successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying migrations");
            throw;
        }
    }
}