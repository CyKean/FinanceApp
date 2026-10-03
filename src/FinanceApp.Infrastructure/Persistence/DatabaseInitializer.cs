namespace FinanceApp.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class DatabaseInitializer
{
    private static bool s_extraColumnsEnsured;
    private static readonly object s_extraColumnsLock = new();
    private static bool s_localUsersTableEnsured;
    private static readonly object s_localUsersTableLock = new();

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
            EnsureColumn(connection, "Budgets", "LinkedAccountId", "TEXT");
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

    /// <summary>
    /// Creates the LocalUsers table on databases that were created before local
    /// (offline) accounts existed. EnsureCreated is a no-op once any table
    /// exists, so it will never add this one to an existing install - without
    /// this, registration would throw a "no such table" on upgraded apps.
    /// Safe to call on every DbContext construction - runs once per process.
    /// </summary>
    public static void EnsureLocalUsersTable(FinanceAppDbContext context)
    {
        lock (s_localUsersTableLock)
        {
            if (s_localUsersTableEnsured)
                return;
            s_localUsersTableEnsured = true;
        }

        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        try
        {
            if (wasClosed)
                connection.Open();

            // Column names, types and the unique index mirror the EF mapping in
            // OnModelCreating - SQLite is dynamically typed, so a drift here
            // would surface as a runtime query error rather than a build error.
            using var create = connection.CreateCommand();
            create.CommandText =
                """
                CREATE TABLE IF NOT EXISTS "LocalUsers" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_LocalUsers" PRIMARY KEY,
                    "Email" TEXT NOT NULL,
                    "PasswordHash" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "LastLoginAt" TEXT NULL
                )
                """;
            create.ExecuteNonQuery();

            using var index = connection.CreateCommand();
            index.CommandText =
                "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_LocalUsers_Email\" ON \"LocalUsers\" (\"Email\")";
            index.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed)
                connection.Close();
        }
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