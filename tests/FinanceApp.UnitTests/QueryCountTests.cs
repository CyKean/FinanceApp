namespace FinanceApp.UnitTests;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Application.Validators;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

/// <summary>
/// Counts the SQL each read path actually issues, against a real SQLite
/// database and the real repositories.
/// <para>
/// Round-trip counts are the whole point of the query-layer work, and asserting
/// them is the only way they stay true: an N+1 reintroduced in a mapper looks
/// identical in a unit test with mocked repositories and costs a round-trip per
/// row in the app.
/// </para>
/// </summary>
public class QueryCountTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly CommandCounter _counter = new();

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public QueryCountTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_connection);
        services.AddSingleton<DbCommandInterceptor>(_counter);
        services.AddDbContext<FinanceAppDbContext>((sp, options) =>
        {
            options.UseSqlite(sp.GetRequiredService<SqliteConnection>());
            options.AddInterceptors(sp.GetRequiredService<DbCommandInterceptor>());
            options.UseLoggerFactory(NullLoggerFactory.Instance);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IFinancialGoalRepository, FinancialGoalRepository>();
        services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();
        services.AddScoped<ISyncOperationRepository, SyncOperationRepository>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
    }

    private T Resolve<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    private DashboardService BuildDashboardService()
    {
        var sp = _scope.ServiceProvider;
        return new DashboardService(
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<IAccountRepository>(),
            sp.GetRequiredService<ITransactionRepository>(),
            sp.GetRequiredService<ICategoryRepository>(),
            sp.GetRequiredService<IBudgetRepository>(),
            sp.GetRequiredService<IFinancialGoalRepository>(),
            NullLogger<DashboardService>.Instance);
    }

    private TransactionService BuildTransactionService()
    {
        var sp = _scope.ServiceProvider;
        return new TransactionService(
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<ITransactionRepository>(),
            sp.GetRequiredService<IAccountRepository>(),
            sp.GetRequiredService<ICategoryRepository>(),
            Mock.Of<IBudgetService>(),
            new CreateTransactionDtoValidator(),
            new UpdateTransactionDtoValidator(),
            new TransactionFilterDtoValidator(),
            NullLogger<TransactionService>.Instance);
    }

    private async Task SeedAsync(int transactionCount, int categoryCount = 6, int budgetCount = 3)
    {
        var context = Resolve<FinanceAppDbContext>();

        var account = new Account("Cash", AccountType.Cash, new Money(5000), UserId);
        context.Accounts.Add(account);

        var categories = new List<Category>();
        for (var i = 0; i < categoryCount; i++)
        {
            var category = new Category($"Cat {i}", CategoryType.Expense, UserId);
            categories.Add(category);
            context.Categories.Add(category);
        }

        var income = new Category("Salary", CategoryType.Income, UserId);
        context.Categories.Add(income);

        var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        for (var i = 0; i < transactionCount; i++)
        {
            var category = i % 5 == 0 ? income : categories[i % categories.Count];

            // Spread over the last 12 months so the dashboard has data in the
            // window it actually queries, rather than a trivially small result.
            context.Transactions.Add(new Transaction(
                category == income ? TransactionType.Income : TransactionType.Expense,
                new Money(10 + (i % 90)),
                start.AddMonths(-(i % 12)).AddDays(i % 28),
                (AccountId)account.Id,
                (CategoryId)category.Id,
                UserId,
                $"note {i}"));
        }

        for (var i = 0; i < budgetCount; i++)
        {
            context.Budgets.Add(new Budget(
                $"Budget {i}",
                new Money(1000),
                start,
                start.AddMonths(1).AddDays(-1),
                (CategoryId)categories[i % categories.Count].Id,
                UserId));
        }

        await context.SaveChangesAsync();

        // Everything above ran unmeasured.
        _counter.Reset();
    }

    [Fact]
    public async Task GetDashboardAsync_StaysAtASmallFixedNumberOfQueries()
    {
        await SeedAsync(transactionCount: 400);
        var service = BuildDashboardService();

        await service.GetDashboardAsync(UserId);

        // Measured at 13: total balance, income, expense, recent page, accounts,
        // categories, category totals, monthly totals, budgets, goals, plus the
        // batched name lookups. Was ~55 round-trips and a write transaction.
        Assert.True(_counter.Total <= 15,
            $"Dashboard issued {_counter.Total} commands: {string.Join(" | ", _counter.Describe())}");

        Assert.Equal(0, _counter.Writes);
    }

    [Fact]
    public async Task GetDashboardAsync_DoesNotWriteWhileReading()
    {
        await SeedAsync(transactionCount: 200);
        var service = BuildDashboardService();

        await service.GetDashboardAsync(UserId);

        // A read that dirties rows takes SQLite's write lock and queues a sync
        // operation for a change the server never made.
        Assert.Equal(0, _counter.Writes);
        Assert.DoesNotContain(_counter.Commands, c => c.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetAnalyticsAsync_StaysAtASmallFixedNumberOfQueries()
    {
        await SeedAsync(transactionCount: 400);
        var service = BuildDashboardService();

        await service.GetAnalyticsAsync(UserId, months: 6);

        // Same shape as the dashboard: 2 sums, trends, category totals, lookups.
        Assert.True(_counter.Total <= 15,
            $"Analytics issued {_counter.Total} commands: {string.Join(" | ", _counter.Describe())}");

        Assert.Equal(0, _counter.Writes);
    }

    [Fact]
    public async Task GetAllAsync_QueryCountDoesNotGrowWithTransactionCount()
    {
        // The whole point of moving paging into SQL: page 1 used to read the
        // entire history regardless of PageSize.
        await SeedAsync(transactionCount: 20);
        var service = BuildTransactionService();
        await service.GetAllAsync(UserId, new TransactionFilterDto(Page: 1, PageSize: 20));
        var withFewRows = _counter.Total;

        await SeedAsync(transactionCount: 500);
        await service.GetAllAsync(UserId, new TransactionFilterDto(Page: 1, PageSize: 20));
        var withManyRows = _counter.Total;

        Assert.Equal(withFewRows, withManyRows);
        Assert.True(withManyRows <= 6,
            $"Transaction page issued {withManyRows} commands: {string.Join(" | ", _counter.Describe())}");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyThePage()
    {
        await SeedAsync(transactionCount: 60);
        var service = BuildTransactionService();

        var page = await service.GetAllAsync(UserId, new TransactionFilterDto(Page: 1, PageSize: 20));

        Assert.Equal(20, page.Count);
    }

    [Fact]
    public async Task GetSummaryAsync_CountsInSqlRatherThanLoadingRows()
    {
        await SeedAsync(transactionCount: 120);
        var service = BuildTransactionService();

        // Seeded rows land on days 0-27 of the last 12 months; the current
        // month therefore holds exactly transactionCount / 12 of them.
        var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var summary = await service.GetSummaryAsync(
            UserId, startOfMonth, startOfMonth.AddMonths(1).AddDays(-1));

        Assert.Equal(120 / 12, summary.TransactionCount);
        // 2 sums + 1 count, not 2 sums plus a full table scan materialised into a
        // List just to call .Count() on it.
        Assert.True(_counter.Total <= 4, $"Summary issued {_counter.Total} commands");
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private readonly List<string> _commands = new();

        public IReadOnlyList<string> Commands
        {
            get
            {
                lock (_commands) return _commands.ToList();
            }
        }

        public int Total => Commands.Count;

        public int Writes => Commands.Count(c =>
            c.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ||
            c.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ||
            c.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase));

        public IEnumerable<string> Describe() => Commands.Select(c => c.Length > 90 ? c[..90] : c);

        public void Reset()
        {
            lock (_commands) _commands.Clear();
        }

        private void Record(DbCommand command)
        {
            lock (_commands) _commands.Add(command.CommandText.Trim());
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Record(command);
            return result;
        }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Record(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Record(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Record(command);
            return ValueTask.FromResult(result);
        }
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}