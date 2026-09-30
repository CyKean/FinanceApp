namespace FinanceApp.Mobile.Services;

using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Infrastructure.Configuration;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Development-phase demo data seeder. When Database:SeedDemoData is true and the
/// signed-in user has no accounts and no transactions yet, it populates ~12 months
/// of realistic PHP data so charts and dashboards are not empty during development.
/// </summary>
public class DevDataSeeder
{
    private readonly IServiceProvider _services;
    private readonly IOptions<DatabaseOptions> _options;
    private readonly ILogger<DevDataSeeder> _logger;

    public DevDataSeeder(IServiceProvider services, IOptions<DatabaseOptions> options, ILogger<DevDataSeeder> logger)
    {
        _services = services;
        _options = options;
        _logger = logger;
    }

    public async Task SeedIfEmptyAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_options.Value.SeedDemoData)
            return;

        try
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FinanceAppDbContext>();

            var hasData = await context.Transactions.AnyAsync(t => t.UserId == userId, cancellationToken);
            if (hasData)
                return;

            var categoryService = scope.ServiceProvider.GetRequiredService<ICategoryService>();
            await categoryService.InitializeDefaultCategoriesAsync(userId, cancellationToken);

            var categories = await context.Categories
                .Where(c => c.UserId == userId)
                .ToListAsync(cancellationToken);

            var rng = new Random(42);
            var today = DateTime.UtcNow.Date;
            var currentMonth = new DateTime(today.Year, today.Month, 1);

            var cash = new Account("Cash Wallet", AccountType.Cash, new Money(3000), userId,
                "Daily expenses", "💵", "#0E6B4F", isDefault: false, sortOrder: 0);
            var bank = new Account("BDO Savings", AccountType.Bank, new Money(35000), userId,
                "Primary bank account", "🏦", "#118AB2", isDefault: true, sortOrder: 1);
            var ewallet = new Account("GCash Wallet", AccountType.EWallet, new Money(1500), userId,
                "Mobile wallet", "📱", "#3A86FF", isDefault: false, sortOrder: 2);

            var accountsById = new Dictionary<Guid, Account>
            {
                [cash.Id] = cash,
                [bank.Id] = bank,
                [ewallet.Id] = ewallet
            };

            var food = Require(categories, "Food", CategoryType.Expense);
            var transportation = Require(categories, "Transportation", CategoryType.Expense);
            var housing = Require(categories, "Housing", CategoryType.Expense);
            var utilities = Require(categories, "Utilities", CategoryType.Expense);
            var shopping = Require(categories, "Shopping", CategoryType.Expense);
            var entertainment = Require(categories, "Entertainment", CategoryType.Expense);
            var health = Require(categories, "Health", CategoryType.Expense);
            var bills = Require(categories, "Bills", CategoryType.Expense);
            var subscriptions = Require(categories, "Subscriptions", CategoryType.Expense);
            var salaryCategory = Require(categories, "Salary", CategoryType.Income);
            var freelanceCategory = Require(categories, "Freelance", CategoryType.Income);

            var transactions = new List<Transaction>();

            void Add(TransactionType type, decimal amount, DateTime date, Account account, Category category, string notes)
            {
                transactions.Add(new Transaction(type, new Money(amount), date,
                    new AccountId(account.Id), new CategoryId(category.Id), userId, notes));
            }

            for (var offset = 11; offset >= 0; offset--)
            {
                var monthStart = currentMonth.AddMonths(-offset);
                var maxDay = offset == 0
                    ? today.Day
                    : DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

                DateTime OnDay(int day) => monthStart.AddDays(Math.Min(day, maxDay) - 1);
                DateTime RandomDay() => monthStart.AddDays(rng.Next(1, maxDay + 1) - 1);

                Add(TransactionType.Income, 48000m + rng.Next(-20, 21) * 100, OnDay(15), bank, salaryCategory, "Monthly salary");
                if (rng.Next(100) < 50)
                    Add(TransactionType.Income, rng.Next(3000, 12001), RandomDay(), ewallet, freelanceCategory, "Freelance project");

                Add(TransactionType.Expense, 12000, OnDay(3), bank, housing, "Apartment rent");
                Add(TransactionType.Expense, rng.Next(1200, 3001), OnDay(8), bank, utilities, "Electricity & water bill");
                Add(TransactionType.Expense, 1299, OnDay(10), bank, bills, "Internet & phone bill");
                Add(TransactionType.Expense, rng.Next(300, 501), OnDay(15), ewallet, subscriptions, "Streaming subscriptions");

                var groceryRuns = rng.Next(2, 4);
                for (var i = 0; i < groceryRuns; i++)
                    Add(TransactionType.Expense, rng.Next(800, 2501), RandomDay(),
                        rng.Next(100) < 70 ? bank : cash, food, "Groceries");

                var foodRuns = rng.Next(3, 6);
                for (var i = 0; i < foodRuns; i++)
                    Add(TransactionType.Expense, rng.Next(150, 701), RandomDay(),
                        rng.Next(100) < 60 ? cash : ewallet, food, "Lunch, coffee & food delivery");

                var transportRuns = rng.Next(4, 7);
                for (var i = 0; i < transportRuns; i++)
                    Add(TransactionType.Expense, rng.Next(40, 351), RandomDay(), cash, transportation, "Jeepney & Grab fare");

                if (rng.Next(100) < 60)
                    Add(TransactionType.Expense, rng.Next(500, 3001), RandomDay(), ewallet, shopping, "Clothes & gadgets");
                if (rng.Next(100) < 70)
                    Add(TransactionType.Expense, rng.Next(200, 901), RandomDay(), cash, entertainment, "Movies & games");
                if (rng.Next(100) < 15)
                    Add(TransactionType.Expense, rng.Next(400, 1801), RandomDay(), cash, health, "Pharmacy & clinic visit");
            }

            foreach (var transaction in transactions)
            {
                var account = accountsById[transaction.AccountId.Value];
                var delta = transaction.Type == TransactionType.Income
                    ? transaction.Amount
                    : new Money(-transaction.Amount.Amount);
                account.AdjustBalance(delta);
            }

            var monthEnd = currentMonth.AddMonths(1).AddDays(-1);
            var budgets = new List<Budget>
            {
                new("Food & Groceries", new Money(18000), currentMonth, monthEnd,
                    new CategoryId(food.Id), userId, food.Icon, food.Color, null),
                new("Transportation", new Money(5000), currentMonth, monthEnd,
                    new CategoryId(transportation.Id), userId, transportation.Icon, transportation.Color, null),
                new("Shopping", new Money(5000), currentMonth, monthEnd,
                    new CategoryId(shopping.Id), userId, shopping.Icon, shopping.Color, null),
                new("Entertainment", new Money(3000), currentMonth, monthEnd,
                    new CategoryId(entertainment.Id), userId, entertainment.Icon, entertainment.Color, null)
            };

            var goal = new FinancialGoal("Emergency Fund", new Money(100000), today.AddMonths(6), userId,
                startDate: today.AddMonths(-1),
                description: "Aim for 3-6 months of living expenses",
                icon: "🛡️", color: "#0E6B4F", linkedAccountId: null);
            goal.AddProgress(new Money(25000));

            var laptopGoal = new FinancialGoal("New Laptop", new Money(60000), today.AddMonths(4), userId,
                startDate: today.AddMonths(-1),
                description: "Upgrade work laptop",
                icon: "💻", color: "#3A86FF", linkedAccountId: null);
            laptopGoal.AddProgress(new Money(18000));

            var allowanceCategory = Require(categories, "Allowance", CategoryType.Income);
            var recurring = new List<RecurringTransaction>
            {
                new("Netflix & Spotify", TransactionType.Expense, new Money(499),
                    RecurringFrequency.Monthly, currentMonth,
                    new AccountId(ewallet.Id), new CategoryId(subscriptions.Id), userId, "Auto-debit"),
                new("Gym Membership", TransactionType.Expense, new Money(1200),
                    RecurringFrequency.Monthly, currentMonth,
                    new AccountId(bank.Id), new CategoryId(health.Id), userId, "Monthly gym dues"),
                new("Family Allowance", TransactionType.Income, new Money(5000),
                    RecurringFrequency.Monthly, currentMonth,
                    new AccountId(bank.Id), new CategoryId(allowanceCategory.Id), userId)
            };

            context.AddRange(cash, bank, ewallet);
            context.AddRange(transactions);
            context.AddRange(budgets);
            context.AddRange(goal, laptopGoal);
            context.AddRange(recurring);
            await context.SaveChangesAsync(cancellationToken);

            // NOTE: outbox rows for seeded data are intentionally kept - the sync
            // backfill picks them up so seeds upload like everything else.

            _logger.LogInformation(
                "Seeded demo data for user {UserId}: {Accounts} accounts, {Transactions} transactions, {Budgets} budgets, 2 goals, {Recurring} recurring",
                userId, accountsById.Count, transactions.Count, budgets.Count, recurring.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dev data seeding failed for user {UserId}", userId);
        }
    }

    private static Category Require(IEnumerable<Category> categories, string name, CategoryType type)
    {
        return categories.FirstOrDefault(c => c.Name == name && c.Type == type)
            ?? throw new InvalidOperationException($"Default category '{name}' ({type}) is missing");
    }
}
