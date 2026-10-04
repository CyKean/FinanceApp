namespace FinanceApp.UnitTests;

using System;
using System.IO;
using System.Threading.Tasks;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// What happens to an install that was created before balances were derived.
/// <para>
/// The opening balance is the one balance input that cannot be worked out from
/// transactions, so it is stored. An account created by an older build has no
/// such column, and the upgrade has to give it one seeded with the balance the
/// user already sees - otherwise every existing account would restart from zero
/// and the transactions already recorded against it would be counted against
/// nothing.
/// </para>
/// <para>
/// This builds the pre-upgrade table by hand and opens one context over it,
/// which is the only way to exercise the migration: EnsureCreated leaves an
/// existing database alone, so the column has to be added the way an upgrade
/// adds it.
/// </para>
/// </summary>
public class AccountBalanceUpgradeTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public AccountBalanceUpgradeTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"financeapp-upgrade-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_databasePath}";
        CreatePreUpgradeDatabase();
    }

    /// <summary>The Accounts table exactly as the release that shipped this bug had it.</summary>
    private void CreatePreUpgradeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        Execute(connection,
            """
            CREATE TABLE "Accounts" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Accounts" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "Type" INTEGER NOT NULL,
                "Balance" TEXT NOT NULL,
                "BalanceCurrency" TEXT NOT NULL DEFAULT 'PHP',
                "Description" TEXT NULL,
                "Icon" TEXT NULL,
                "Color" TEXT NULL,
                "UserId" TEXT NOT NULL,
                "IsDefault" INTEGER NOT NULL DEFAULT 0,
                "SortOrder" INTEGER NOT NULL DEFAULT 0,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "IsDeleted" INTEGER NOT NULL DEFAULT 0,
                "Version" INTEGER NOT NULL DEFAULT 1,
                "SyncStatus" INTEGER NOT NULL,
                "LastSyncedAt" TEXT NULL
            )
            """);

        // EnsureExtraColumns patches this table too, so it has to be present.
        Execute(connection, """CREATE TABLE "Budgets" ("Id" TEXT NOT NULL CONSTRAINT "PK_Budgets" PRIMARY KEY)""");

        // An account that already spent 100 of its 200, and is showing 100.
        // Synced, because it has been through a sync: the state the bug needs.
        Execute(connection,
            $"""
            INSERT INTO "Accounts"
                ("Id", "Name", "Type", "Balance", "BalanceCurrency", "UserId", "IsDefault", "SortOrder", "CreatedAt", "UpdatedAt", "IsDeleted", "Version", "SyncStatus", "LastSyncedAt")
            VALUES
                ('{_cashId}', 'Cash', 0, '100.00', 'PHP', '{UserId}', 1, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00', 0, 3, 0, '2026-01-01 00:05:00')
            """);
    }

    private static readonly Guid _cashId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    [Fact]
    public async Task AnExistingAccount_GainsAnOpeningBalance_AndKeepsTheBalanceItShowed()
    {
        var options = new DbContextOptionsBuilder<FinanceAppDbContext>()
            .UseSqlite(_connectionString)
            .Options;

        // Opening the context is what runs the upgrade.
        using var context = new FinanceAppDbContext(options);
        var repository = new AccountRepository(context);

        var cash = Assert.Single(await repository.GetByUserIdAsync(UserId));

        // Seeded from the balance that was there, so the balance the user already
        // sees does not move and transactions are counted on top of it rather
        // than from zero.
        Assert.Equal(100m, cash.InitialBalanceAmount);
        Assert.Equal(100m, cash.Balance.Amount);
        Assert.Equal("Cash", cash.Name);
        Assert.True(cash.IsDefault);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // A leftover temp file is not worth failing a test over.
        }

        GC.SuppressFinalize(this);
    }
}