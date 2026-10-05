namespace FinanceApp.UnitTests;

using System;
using System.Collections.Generic;
using System.Threading;
using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Application.Validators;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Infrastructure.Configuration;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Repositories;
using FinanceApp.Infrastructure.Supabase;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

/// <summary>
/// One install of the app: a database and the services over it, pointed at a
/// shared <see cref="PostgrestStub"/> so two of them can sync with each other.
/// <para>
/// Everything shares a single DbContext on purpose. EF only saves what is
/// tracked by the context whose SaveChanges is called, so wiring services
/// against two contexts over one database writes nothing and turns the test
/// into a no-op that passes for the wrong reason.
/// </para>
/// </summary>
internal sealed class TestDevice : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FinanceAppDbContext _context;
    private readonly SupabaseClientProvider _cloud;

    public TestDevice(PostgrestStub cloud, Guid? userId = null)
    {
        Cloud = cloud;
        _cloud = new SupabaseClientProvider(
            Options.Create(new DatabaseOptions
            {
                SupabaseUrl = cloud.Url,
                SupabaseAnonKey = cloud.AnonKey
            }),
            NullLogger<SupabaseClientProvider>.Instance);

        if (userId is { } id)
            SignIn(id);

        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new FinanceAppDbContext(
            new DbContextOptionsBuilder<FinanceAppDbContext>().UseSqlite(_connection).Options);

        Accounts = new AccountRepository(_context);
        Categories = new CategoryRepository(_context);
        Transactions = new TransactionRepository(_context);
        SyncOps = new SyncOperationRepository(_context);

        Work = new UnitOfWork(
            _context,
            Accounts,
            Categories,
            Transactions,
            new BudgetRepository(_context),
            new RecurringTransactionRepository(_context),
            new FinancialGoalRepository(_context),
            SyncOps);

        Balances = new AccountBalanceService(
            Work, Accounts, Transactions, NullLogger<AccountBalanceService>.Instance);
    }

    public AccountRepository Accounts { get; }
    public CategoryRepository Categories { get; }
    public TransactionRepository Transactions { get; }
    public SyncOperationRepository SyncOps { get; }
    public IUnitOfWork Work { get; }
    public AccountBalanceService Balances { get; }
    public PostgrestStub Cloud { get; }

    public ISupabaseSyncService Transport() =>
        new SupabaseSyncService(
            _cloud,
            Work,
            Balances,
            NullLogger<SupabaseSyncService>.Instance);

    private void SignIn(Guid userId)
    {
        var client = _cloud.TryGetClientAsync().GetAwaiter().GetResult()
            ?? throw new InvalidOperationException("The cloud stub did not come up.");

        client.Auth.SetSession(PostgrestStub.SessionTokenFor(userId.ToString()), "stub-refresh-token")
            .GetAwaiter().GetResult();
    }

    public SyncService Sync() =>
        new SyncService(
            Work,
            SyncOps,
            Transactions,
            Accounts,
            Categories,
            new BudgetRepository(_context),
            new RecurringTransactionRepository(_context),
            new FinancialGoalRepository(_context),
            Transport(),
            Online(),
            NullLogger<SyncService>.Instance);

    public AccountService Accounts_() =>
        new AccountService(
            Work,
            Accounts,
            Transactions,
            new BudgetRepository(_context),
            new FinancialGoalRepository(_context),
            Mock.Of<ICategoryService>(),
            new CreateAccountDtoValidator(),
            new UpdateAccountDtoValidator(),
            NullLogger<AccountService>.Instance);

    private static IConnectivityService Online()
    {
        var connectivity = new Mock<IConnectivityService>();
        connectivity.Setup(x => x.CheckConnectivityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(NetworkAccess.Internet);
        return connectivity.Object;
    }

    public List<(string Name, decimal Balance)> ReadAccounts()
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            "SELECT \"Name\", \"Balance\" FROM \"Accounts\" WHERE \"IsDeleted\" = 0 ORDER BY \"Name\", \"Balance\"";
        using var reader = command.ExecuteReader();
        var rows = new List<(string, decimal)>();
        while (reader.Read())
            rows.Add((reader.GetString(0), Convert.ToDecimal(reader.GetValue(1))));
        return rows;
    }

    public int CountDefaults()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"Accounts\" WHERE \"IsDefault\" = 1 AND \"IsDeleted\" = 0";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public int CountTransactions() => CountLive("Transactions");

    /// <summary>Live rows of a table - a merged-away spare counts zero here.</summary>
    public int CountLive(string table) => ExecuteScalar($"SELECT COUNT(*) FROM \"{table}\" WHERE \"IsDeleted\" = 0");

    /// <summary>Rows of a table regardless of the tombstone, so a merge can be
    /// seen to have retired the spare rather than destroyed it - destroying it
    /// would let the next pull bring the server copy straight back.</summary>
    public int CountAnyState(string table) => ExecuteScalar($"SELECT COUNT(*) FROM \"{table}\"");

    /// <summary>Ids of the live rows of a table.</summary>
    public List<string> LiveIds(string table)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT \"Id\" FROM \"{table}\" WHERE \"IsDeleted\" = 0";
        using var reader = command.ExecuteReader();
        var ids = new List<string>();
        while (reader.Read())
            ids.Add(reader.GetString(0));
        return ids;
    }

    /// <summary>What each surviving transaction points at, so a merge can be seen
    /// to have moved its references onto the survivor rather than lost them.</summary>
    public List<(string AccountId, string CategoryId)> TransactionLinks()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT \"AccountId\", \"CategoryId\" FROM \"Transactions\" WHERE \"IsDeleted\" = 0";
        using var reader = command.ExecuteReader();
        var links = new List<(string, string)>();
        while (reader.Read())
            links.Add((reader.GetString(0), reader.GetString(1)));
        return links;
    }

    private int ExecuteScalar(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
