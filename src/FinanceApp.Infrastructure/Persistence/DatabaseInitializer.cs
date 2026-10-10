namespace FinanceApp.Infrastructure.Persistence;

using System.Collections.Concurrent;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class DatabaseInitializer
{
    // Keyed by database rather than a single flag: a process can hold more than
    // one database (an upgrade running alongside live data, a test harness), and a
    // global flag would leave the second one unpatched.
    private static readonly ConcurrentDictionary<string, bool> s_extraColumnsEnsured = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object s_extraColumnsLock = new();
    private static bool s_localUsersTableEnsured;
    private static readonly object s_localUsersTableLock = new();
    private static readonly ConcurrentDictionary<string, bool> s_schemaEnsured = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object s_schemaLock = new();

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
    /// Creates the schema for a database that has no tables yet.
    /// <para>
    /// EF does not cache <c>EnsureCreated</c>: every call probes sqlite_master.
    /// This context is Scoped, and the app creates a scope per query (see
    /// BaseViewModel.QueryOffUiThreadAsync) and per sync tick, so leaving the
    /// probe in the constructor meant paying it on every single database access.
    /// </para>
    /// <para>
    /// The result is cached against a data source, but only when that data source
    /// identifies a private file on disk. ":memory:" and shared-cache connections
    /// all report the same data source string while being entirely separate
    /// databases - two SQLite connections to ":memory:" cannot even see each
    /// other's tables - so caching against those would leave every database after
    /// the first one without a schema. The file check also means a deleted
    /// database is recreated rather than assumed present.
    /// </para>
    /// </summary>
    public static void EnsureSchema(FinanceAppDbContext context)
    {
        var dataSource = context.Database.GetDbConnection().DataSource;

        if (!IsCacheableDataSource(dataSource))
        {
            context.Database.EnsureCreated();
            return;
        }

        lock (s_schemaLock)
        {
            if (s_schemaEnsured.ContainsKey(dataSource) && File.Exists(dataSource))
                return;

            s_schemaEnsured[dataSource] = true;
        }

        try
        {
            context.Database.EnsureCreated();
        }
        catch
        {
            // A later resolution has to be able to retry: a database that could
            // not be created is not one that needs creating again.
            lock (s_schemaLock)
            {
                s_schemaEnsured.TryRemove(dataSource, out _);
            }

            throw;
        }
    }

    /// <summary>
    /// Whether a data source names a private file that can be used as a cache
    /// key. Anything the SQLite connection string can make resolve to a shared or
    /// transient database is excluded.
    /// </summary>
    private static bool IsCacheableDataSource(string? dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
            return false;

        if (dataSource.Contains(';') || dataSource.Contains('=', StringComparison.Ordinal))
            return false;

        if (dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return false;

        return !dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Adds columns introduced after v1 to databases created by older builds.
    /// EnsureCreated only creates missing TABLES, never missing columns.
    /// Safe to call on every DbContext construction - runs once per database.
    /// </summary>
    public static void EnsureExtraColumns(FinanceAppDbContext context)
    {
        var connection = context.Database.GetDbConnection();

        lock (s_extraColumnsLock)
        {
            if (!s_extraColumnsEnsured.TryAdd(connection.DataSource, true))
                return;
        }

        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        try
        {
            if (wasClosed)
                connection.Open();

            EnsureColumn(connection, "Budgets", "Icon", "TEXT");
            EnsureColumn(connection, "Budgets", "Color", "TEXT");
            EnsureColumn(connection, "Budgets", "LinkedAccountId", "TEXT");

            // Balances are now derived from an account's opening balance plus its
            // transactions, so an existing install has to keep the balance it
            // already shows as the opening balance - less the movements already
            // counted into it, or the first recalculation would count them twice
            // and an account that had spent 100 of 200 would reappear at zero.
            // Seeding only happens on the run that adds the column, so it cannot
            // later mistake a re-opened account for an unmigrated one.
            if (EnsureColumn(connection, "Accounts", "InitialBalance", "NOT NULL DEFAULT 0"))
            {
                using var seed = connection.CreateCommand();
                seed.CommandText = TableExists(connection, "Transactions")
                    ? """
                      UPDATE "Accounts" SET "InitialBalance" = "Balance" - COALESCE((
                          SELECT SUM(CASE WHEN "Type" = 1 THEN "Amount" ELSE -"Amount" END)
                          FROM "Transactions"
                          WHERE "Transactions"."AccountId" = "Accounts"."Id"
                            AND "Transactions"."IsDeleted" = 0
                            AND "Transactions"."Currency" = "Accounts"."BalanceCurrency"
                      ), 0)
                      """
                    : "UPDATE \"Accounts\" SET \"InitialBalance\" = \"Balance\"";
                seed.ExecuteNonQuery();
            }
        }
        finally
        {
            if (wasClosed)
                connection.Close();
        }
    }

    private static bool EnsureColumn(System.Data.Common.DbConnection connection, string table, string column, string type)
    {
        using var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info(\"{table}\")";
        using var reader = pragma.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                reader.Close();
                return false;
            }
        }
        reader.Close();

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}";
        alter.ExecuteNonQuery();
        return true;
    }

    /// <summary>
    /// Whether a table is there to query. The backfill above reads across to
    /// Transactions, and an unguarded read of a table the database has never had
    /// would fail the whole upgrade rather than just skipping the subtraction.
    /// </summary>
    private static bool TableExists(System.Data.Common.DbConnection connection, string table)
    {
        using var lookup = connection.CreateCommand();
        lookup.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name";
        var parameter = lookup.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = table;
        lookup.Parameters.Add(parameter);
        return Convert.ToInt32(lookup.ExecuteScalar()) > 0;
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