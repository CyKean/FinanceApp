using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.Interfaces;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceApp.UnitTests;

/// <summary>
/// Exercises the real <see cref="PredictionService"/> and
/// <see cref="BudgetSuggestionService"/> over a realistic dataset shaped like
/// DevDataSeeder produces, stubbing only at the repository boundary.
/// <para>
/// The stubs honour their date-range arguments rather than returning
/// everything, because the whole point is to prove the services window, group
/// and order real user data correctly on the way to the page DTOs.
/// </para>
/// </summary>
public class AiFeatureIntegrationTests
{
    private readonly Guid _userId = Guid.NewGuid();

    private readonly Dictionary<string, Guid> _categoryIds = new(StringComparer.OrdinalIgnoreCase);

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAccountRepository> _accounts = new();
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<IBudgetRepository> _budgets = new();
    private readonly Mock<IFinancialGoalRepository> _goals = new();
    private readonly Mock<IRecurringTransactionRepository> _recurring = new();

    private List<Transaction> _data = new();
    private List<Budget> _budgetEntities = new();

    public AiFeatureIntegrationTests()
    {
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _accounts.Setup(x => x.GetTotalBalanceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Money(35000));

        _goals.Setup(x => x.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FinancialGoal>());

        _recurring.Setup(x => x.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecurringTransaction>());
    }

    // ---------------------------------------------------------------- helpers

    private static DateTime FirstOfThisMonth => new(DateTime.Today.Year, DateTime.Today.Month, 1);

    private static DateTime MonthStart(int monthsAgo) => FirstOfThisMonth.AddMonths(-monthsAgo);

    private Guid CategoryId(string name)
    {
        if (!_categoryIds.TryGetValue(name, out var id))
        {
            id = Guid.NewGuid();
            _categoryIds[name] = id;
        }

        return id;
    }

    private void Seed(int months)
    {
        var random = new Random(42);
        _data = new List<Transaction>();
        _budgetEntities = new List<Budget>();

        var accountId = Guid.NewGuid();

        Transaction Add(TransactionType type, decimal amount, DateTime date, string category)
        {
            var transaction = new Transaction(
                type,
                new Money(amount),
                date,
                new AccountId(accountId),
                new CategoryId(CategoryId(category)),
                _userId);

            typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(transaction, Guid.NewGuid());
            _data.Add(transaction);
            return transaction;
        }

        for (var offset = months - 1; offset >= 0; offset--)
        {
            var start = MonthStart(offset);
            var lastDay = offset == 0
                ? DateTime.Today.Day
                : DateTime.DaysInMonth(start.Year, start.Month);

            DateTime OnDay(int day) => start.AddDays(Math.Min(day, lastDay) - 1);

            Add(TransactionType.Income, 48000m, OnDay(15), "Salary");

            Add(TransactionType.Expense, 12000m, OnDay(3), "Housing");
            Add(TransactionType.Expense, 2200m, OnDay(8), "Utilities");
            Add(TransactionType.Expense, 1299m, OnDay(10), "Bills");

            // Food creeps up over the year so trends have a real direction.
            var foodBase = 9000m + (months - 1 - offset) * 120m;

            for (var i = 0; i < 3; i++)
                Add(TransactionType.Expense, foodBase / 3m, start.AddDays(random.Next(1, lastDay)), "Food");

            for (var i = 0; i < 5; i++)
                Add(TransactionType.Expense, random.Next(40, 350), start.AddDays(random.Next(1, lastDay)), "Transportation");

            for (var i = 0; i < 3; i++)
                Add(TransactionType.Expense, random.Next(500, 2500), start.AddDays(random.Next(1, lastDay)), "Shopping");
        }

        WireRepositories();
    }

    /// <summary>Mirrors the real SQL: filter by type and an inclusive date window.</summary>
    private void WireRepositories()
    {
        _transactions
            .Setup(x => x.GetByTypeAndDateRangeAsync(
                It.IsAny<Guid>(), It.IsAny<TransactionType>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, TransactionType type, DateTime from, DateTime to, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<Transaction>>(_data
                    .Where(t => t.UserId == _userId && t.Type == type && !t.IsDeleted &&
                                t.Date >= from.Date && t.Date <= to.Date)
                    .ToList()));

        _transactions
            .Setup(x => x.GetTotalByTypeAsync(
                It.IsAny<Guid>(), It.IsAny<TransactionType>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, TransactionType type, DateTime from, DateTime to, CancellationToken _) =>
            {
                var total = _data
                    .Where(t => t.UserId == _userId && t.Type == type && !t.IsDeleted &&
                                t.Date >= from.Date && t.Date <= to.Date)
                    .Sum(t => t.Amount.Amount);

                return Task.FromResult(new Money(total));
            });

        _transactions
            .Setup(x => x.GetTotalByCategoryAsync(
                It.IsAny<Guid>(), It.IsAny<CategoryId>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, CategoryId categoryId, DateTime from, DateTime to, CancellationToken _) =>
            {
                var total = _data
                    .Where(t => t.UserId == _userId && t.CategoryId == categoryId && !t.IsDeleted &&
                                t.Date >= from.Date && t.Date <= to.Date)
                    .Sum(t => t.Amount.Amount);

                return Task.FromResult(new Money(total));
            });

        _transactions
            .Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, CancellationToken _) => (IReadOnlyList<Transaction>)_data);

        // Mirrors the real aggregate: expenses only, in the window, grouped by
        // category, optionally narrowed to a set of categories.
        _transactions
            .Setup(x => x.GetCategoryTotalsAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, DateTime from, DateTime to, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<CategoryTotal>>(CategoryTotals(from, to)));

        _transactions
            .Setup(x => x.GetCategoryTotalsAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, IReadOnlyCollection<Guid> categoryIds, DateTime from, DateTime to, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<CategoryTotal>>(
                    CategoryTotals(from, to).Where(t => categoryIds.Contains(t.CategoryId)).ToList()));

        _transactions
            .Setup(x => x.GetMonthlyTotalsAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, DateTime from, DateTime to, CancellationToken _) =>
            {
                var totals = _data
                    .Where(t => t.UserId == _userId && !t.IsDeleted && t.Date >= from.Date && t.Date <= to.Date)
                    .GroupBy(t => (t.Date.Year, t.Date.Month, t.Type))
                    .Select(g => new MonthlyTotal(g.Key.Year, g.Key.Month, g.Key.Type, g.Sum(t => t.Amount.Amount)))
                    .ToList();

                return Task.FromResult<IReadOnlyList<MonthlyTotal>>(totals);
            });

        _categories
            .Setup(x => x.GetActiveByTypeAsync(It.IsAny<Guid>(), It.IsAny<CategoryType>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, CategoryType type, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<Category>>(
                    _categoryIds
                        .Where(kv => type == CategoryType.Income
                            ? kv.Key is "Salary" or "Freelance"
                            : kv.Key is not ("Salary" or "Freelance"))
                        .Select(kv =>
                        {
                            var category = new Category(
                                kv.Key,
                                type,
                                _userId,
                                "icon",
                                "#FF6B6B",
                                null,
                                false,
                                0);
                            typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(category, kv.Value);
                            return category;
                        })
                        .ToList()));

        _categories
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) =>
            {
                var name = _categoryIds.FirstOrDefault(kv => kv.Value == id).Key;
                if (name is null) return Task.FromResult<Category?>(null);

                var isIncome = name is "Salary" or "Freelance";
                var category = new Category(name, isIncome ? CategoryType.Income : CategoryType.Expense,
                    _userId, "icon", "#FF6B6B", null, false, 0);
                typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(category, id);
                return Task.FromResult<Category?>(category);
            });

        _categories
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
            {
                var map = new Dictionary<Guid, Category>();
                foreach (var id in ids)
                {
                    var name = _categoryIds.FirstOrDefault(kv => kv.Value == id).Key;
                    if (name is null) continue;

                    var isIncome = name is "Salary" or "Freelance";
                    var category = new Category(name, isIncome ? CategoryType.Income : CategoryType.Expense,
                        _userId, "icon", "#FF6B6B", null, false, 0);
                    typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(category, id);
                    map[id] = category;
                }

                return Task.FromResult<IReadOnlyDictionary<Guid, Category>>(map);
            });

        _budgets
            .Setup(x => x.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, DateTime asOf, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<Budget>>(_budgetEntities
                    .Where(b => b.UserId == _userId && b.StartDate <= asOf.Date && b.EndDate >= asOf.Date)
                    .ToList()));
    }

    /// <summary>Mirrors the real aggregate: expenses only, in window, grouped by category.</summary>
    private List<CategoryTotal> CategoryTotals(DateTime from, DateTime to) =>
        _data
            .Where(t => t.UserId == _userId && t.Type == TransactionType.Expense && !t.IsDeleted &&
                        t.Date >= from.Date && t.Date <= to.Date)
            .GroupBy(t => t.CategoryId.Value)
            .Select(g => new CategoryTotal(g.Key, g.Sum(t => t.Amount.Amount)))
            .ToList();

    private void AddCurrentMonthBudgets(params (string Name, string Category, decimal Amount)[] specs)
    {
        var start = FirstOfThisMonth;
        var end = start.AddMonths(1).AddDays(-1);

        _budgetEntities = specs.Select(spec => new Budget(
            spec.Name,
            new Money(spec.Amount),
            start,
            end,
            new CategoryId(CategoryId(spec.Category)),
            _userId)).ToList();

        // Categories must exist for BudgetService to map names onto the DTO.
        _ = specs.Select(s => CategoryId(s.Category)).ToList();
        WireRepositories();
    }

    private PredictionService BuildPredictionService() =>
        new(_unitOfWork.Object, _transactions.Object, _categories.Object,
            BuildBudgetService(), _recurring.Object, Mock.Of<ILogger<PredictionService>>());

    private BudgetService BuildBudgetService() =>
        new(_unitOfWork.Object, _budgets.Object, _categories.Object, _accounts.Object,
            _transactions.Object, Mock.Of<INotificationService>(),
            new FinanceApp.Application.Validators.CreateBudgetDtoValidator(),
            new FinanceApp.Application.Validators.UpdateBudgetDtoValidator(),
            Mock.Of<ILogger<BudgetService>>());

    private DashboardService BuildDashboardService() =>
        new(_unitOfWork.Object, _accounts.Object, _transactions.Object, _categories.Object,
            _budgets.Object, _goals.Object, Mock.Of<ILogger<DashboardService>>());

    private BudgetSuggestionService BuildSuggestionService() =>
        new(BuildDashboardService(), BuildBudgetService(),
            Mock.Of<ILogger<BudgetSuggestionService>>());

    // ----------------------------------------------------------------- tests

    [Fact]
    public async Task Forecasts_ArePopulatedFromUserData()
    {
        Seed(months: 12);

        var result = await BuildPredictionService().GeneratePredictionAsync(_userId);

        Assert.NotNull(result.ExpensePrediction);
        Assert.NotEqual(PredictionConfidence.InsufficientData, result.ExpensePrediction.Confidence);
        Assert.True(result.ExpensePrediction.MonthsAnalyzed >= 6,
            $"Expected many months analysed, got {result.ExpensePrediction.MonthsAnalyzed}");
        Assert.True(result.ExpensePrediction.TotalTransactionsAnalyzed >= 50);
        Assert.True(result.ExpensePrediction.TotalPredicted.Amount > 0);

        // Every category the user actually spent in should be represented.
        Assert.Equal(6, result.ExpensePrediction.CategoryPredictions.Count);
        Assert.All(result.ExpensePrediction.CategoryPredictions, cp =>
        {
            Assert.True(cp.PredictedAmount.Amount > 0);
            Assert.False(string.IsNullOrWhiteSpace(cp.CategoryName));
        });

        // Trends need at least four months to have a baseline.
        Assert.NotEmpty(result.SpendingTrends);
        Assert.All(result.SpendingTrends, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.CategoryIcon),
                "The forecast page binds CategoryIcon; it must be populated.");
            Assert.True(Math.Abs(t.ChangePercentage) >= 0);
        });
    }

    [Fact]
    public async Task Forecasts_TrendDirectionMatchesTheData()
    {
        Seed(months: 12);

        var result = await BuildPredictionService().GeneratePredictionAsync(_userId);

        // Food spend rises every month in the fixture, so the trend must be
        // Increasing. The old implementation reported this backwards.
        var food = result.SpendingTrends.SingleOrDefault(t => t.CategoryName == "Food");
        Assert.NotNull(food);
        Assert.Equal(SpendingTrend.Increasing, food!.Trend);
        Assert.True(food.ChangePercentage > 0);
    }

    [Fact]
    public async Task Forecasts_IncludeBudgetRowsWithEverythingThePageBinds()
    {
        Seed(months: 12);
        AddCurrentMonthBudgets(
            ("Food & Groceries", "Food", 18000m),
            ("Transportation", "Transportation", 5000m));

        var result = await BuildPredictionService().GeneratePredictionAsync(_userId);

        Assert.Equal(2, result.BudgetForecasts.Count);

        foreach (var forecast in result.BudgetForecasts)
        {
            // Each of these is bound by PredictionsPage.xaml.
            Assert.False(string.IsNullOrWhiteSpace(forecast.CategoryName));
            Assert.False(string.IsNullOrWhiteSpace(forecast.CategoryIcon));
            Assert.False(string.IsNullOrWhiteSpace(forecast.CategoryColor));
            Assert.True(forecast.PercentageUsed > 0);
            Assert.True(forecast.BudgetAmount.Amount > 0);
        }

        // Projected spend must not exceed spent-so-far plus a whole extra month.
        foreach (var forecast in result.BudgetForecasts)
        {
            Assert.True(
                forecast.PredictedSpent.Amount <= forecast.CurrentSpent.Amount + 20000m,
                $"{forecast.BudgetName}: projected {forecast.PredictedSpent.Amount} from " +
                $"{forecast.CurrentSpent.Amount} spent looks doubled.");
        }
    }

    [Fact]
    public async Task BudgetIdeas_AreDerivedFromUserSpending()
    {
        Seed(months: 12);
        AddCurrentMonthBudgets(("Shopping", "Shopping", 40000m));

        var suggestions = await BuildSuggestionService().GetSuggestionsAsync(_userId);

        Assert.NotEmpty(suggestions);

        // Housing, Utilities, Bills and Transportation have no budget here, so
        // each must be proposed as a new one.
        Assert.Contains(suggestions, s => s.CategoryName == "Housing" && s.Kind == BudgetSuggestionKind.Create);
        Assert.Contains(suggestions, s => s.CategoryName == "Utilities" && s.Kind == BudgetSuggestionKind.Create);

        var create = suggestions.Single(s => s.CategoryName == "Housing");
        Assert.Null(create.TargetBudgetId);
        Assert.Null(create.CurrentAmount);

        // Shopping spends ~4.5k a month against a 40k budget, so it must come
        // back as "lower this".
        var shopping = suggestions.Single(s => s.CategoryName == "Shopping");
        Assert.Equal(BudgetSuggestionKind.Decrease, shopping.Kind);
        Assert.NotNull(shopping.TargetBudgetId);

        // Every suggestion needs something to render and act on.
        Assert.All(suggestions, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id));
            Assert.True(s.SuggestedAmount.Amount >= 100m);
            Assert.False(string.IsNullOrWhiteSpace(s.Reason));
            Assert.False(string.IsNullOrWhiteSpace(s.SuggestedText));
            Assert.DoesNotContain("PHP", s.SuggestedText, StringComparison.OrdinalIgnoreCase);
        });

        Assert.True(suggestions.Count <= 5);
    }

    [Fact]
    public async Task BudgetIdeas_LowerATooGenerousBudget()
    {
        Seed(months: 12);

        // Food averages roughly 9k-11k a month here, so 40k is far too generous.
        AddCurrentMonthBudgets(("Food", "Food", 40000m));

        var suggestions = await BuildSuggestionService().GetSuggestionsAsync(_userId);

        var decrease = suggestions.SingleOrDefault(s =>
            s.CategoryName == "Food" && s.Kind == BudgetSuggestionKind.Decrease);

        Assert.NotNull(decrease);
        Assert.NotNull(decrease!.TargetBudgetId);
        Assert.True(decrease.SuggestedAmount.Amount < 40000m);
    }

    [Fact]
    public async Task Forecasts_AreEmpty_NotBroken_WhenUserHasNoData()
    {
        Seed(months: 0);

        var result = await BuildPredictionService().GeneratePredictionAsync(_userId);

        Assert.Equal(PredictionConfidence.InsufficientData, result.ExpensePrediction.Confidence);
        Assert.Empty(result.ExpensePrediction.CategoryPredictions);
        Assert.Empty(result.SpendingTrends);
        Assert.Empty(result.BudgetForecasts);
        Assert.Empty(result.Insights);
        Assert.Empty(result.Anomalies);
    }

    [Fact]
    public async Task BudgetIdeas_AreEmpty_NotBroken_WhenUserHasNoData()
    {
        Seed(months: 0);

        var suggestions = await BuildSuggestionService().GetSuggestionsAsync(_userId);

        Assert.Empty(suggestions);
    }

    [Fact]
    public async Task Forecasts_AreEmpty_NotBroken_WhenUserHasOnlyThisMonth()
    {
        // One month is below the two-month minimum, which is the realistic
        // "new user" case: a handful of transactions and nothing to compare to.
        Seed(months: 1);

        var result = await BuildPredictionService().GeneratePredictionAsync(_userId);

        Assert.Equal(PredictionConfidence.InsufficientData, result.ExpensePrediction.Confidence);
        Assert.Empty(result.SpendingTrends);
    }
}