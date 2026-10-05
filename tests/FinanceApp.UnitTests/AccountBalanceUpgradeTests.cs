namespace FinanceApp.UnitTests;

using System;
using System.IO;
using System.Threading.Tasks;
using FinanceApp.Application.Services;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// What happens to an install that was created before balances were derived.
/// <para>
/// The opening balance is the one balance input that cannot be worked out from
/// transactions, so it is stored. An account created by an older build has no
/// such column, and the upgrade has to give it one seeded from the balance the
/// user already sees - less the movements already counted into that balance, or
/// the first recalculation counts them a second time.
/// </para>
/// <para>
/// This builds the pre-upgrade tables by hand and opens one context over them,
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

    /// <summary>The tables exactly as the release that shipped this bug had them.</summary>
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

        // EnsureExtraColumns patches these two as well, so they have to be present.
        Execute(connection, """CREATE TABLE "Budgets" ("Id" TEXT NOT NULL CONSTRAINT "PK_Budgets" PRIMARY KEY)""");
        Execute(connection,
            """
            CREATE TABLE "Transactions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Transactions" PRIMARY KEY,
                "Type" INTEGER NOT NULL,
                "Amount" TEXT NOT NULL,
                "Currency" TEXT NOT NULL DEFAULT 'PHP',
                "Date" TEXT NOT NULL,
                "Notes" TEXT NULL,
                "AccountId" TEXT NOT NULL,
                "CategoryId" TEXT NOT NULL,
                "UserId" TEXT NOT NULL,
                "RecurringTransactionId" TEXT NULL,
                "SyncStatus" INTEGER NOT NULL DEFAULT 0,
                "LastSyncedAt" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "IsDeleted" INTEGER NOT NULL DEFAULT 0,
                "Version" INTEGER NOT NULL DEFAULT 1
            )
            """);

        // An account that was opened with 200, has spent 100 of it, and is
        // showing 100. Synced, because it has been through a sync: the state the
        // bug needs. The stored balance is what the old build kept, which is
        // opening plus movements.
        Execute(connection,
            $"""
            INSERT INTO "Accounts"
                ("Id", "Name", "Type", "Balance", "BalanceCurrency", "UserId", "IsDefault", "SortOrder", "CreatedAt", "UpdatedAt", "IsDeleted", "Version", "SyncStatus", "LastSyncedAt")
            VALUES
                ('{_cashId}', 'Cash', 0, '100.00', 'PHP', '{UserId}', 1, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00', 0, 3, 0, '2026-01-01 00:05:00')
            """);

        // An expense of 100: positive in the column, with the type carrying the
        // sign, exactly as the app stores them.
        Execute(connection,
            $"""
            INSERT INTO "Transactions"
                ("Id", "Type", "Amount", "Currency", "Date", "Notes", "AccountId", "CategoryId", "UserId", "RecurringTransactionId", "SyncStatus", "LastSyncedAt", "CreatedAt", "UpdatedAt", "IsDeleted", "Version")
            VALUES
                ('{_expenseId}', 0, '100.00', 'PHP', '2026-01-02', 'Groceries', '{_cashId}', '{_foodId}', '{UserId}', NULL, 0, '2026-01-02 00:05:00', '2026-01-02 00:00:00', '2026-01-02 00:00:00', 0, 3)
            """);
    }

    private static readonly Guid _cashId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid _expenseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid _foodId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private FinanceAppDbContext OpenContext()
    {
        var options = new DbContextOptionsBuilder<FinanceAppDbContext>()
            .UseSqlite(_connectionString)
            .Options;

        // Opening the context is what runs the upgrade.
        return new FinanceAppDbContext(options);
    }

    [Fact]
    public async Task AnExistingAccount_GainsAnOpeningBalance_ThatLeavesTheBalanceItShowedAlone()
    {
        using var context = OpenContext();
        var repository = new AccountRepository(context);

        var cash = Assert.Single(await repository.GetByUserIdAsync(UserId));

        // The stored balance already includes the expense, so the opening balance
        // the upgrade seeds has to be that balance less what the transactions
        // added. Seeding the balance itself would count the expense a second time.
        Assert.Equal(200m, cash.InitialBalanceAmount);
        Assert.Equal(100m, cash.Balance.Amount);
        Assert.Equal("Cash", cash.Name);
        Assert.True(cash.IsDefault);
    }

    [Fact]
    public async Task RecalculatingAfterTheUpgrade_KeepsTheBalanceTheUserWasLookingAt()
    {
        using var context = OpenContext();
        var accounts = new AccountRepository(context);
        var transactions = new TransactionRepository(context);
        var balances = new AccountBalanceService(
            new UnitOfWork(
                context,
                accounts,
                new CategoryRepository(context),
                transactions,
                new BudgetRepository(context),
                new RecurringTransactionRepository(context),
                new FinancialGoalRepository(context),
                new SyncOperationRepository(context)),
            accounts,
            transactions,
            NullLogger<AccountBalanceService>.Instance);

        await balances.RecalculateAsync(_cashId);

        var cash = Assert.Single(await accounts.GetByUserIdAsync(UserId));

        // 200 opening less the 100 already spent. The old backfill seeded 100 -
        // the balance, not the opening balance - which put this account at zero.
        Assert.Equal(100m, cash.Balance.Amount);
        Assert.Equal(200m, cash.InitialBalanceAmount);
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
