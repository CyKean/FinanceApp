using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceApp.UnitTests;

/// <summary>
/// Guards the size of what the app sends to the AI provider. Groq's free tier
/// is the default and caps requests at 12K tokens/minute, so prompt bloat is a
/// real failure mode, not just a cost one.
/// </summary>
public class PromptBudgetTests
{
    /// <summary>
    /// Measured ceiling for a user with 4 categories, 6 budgets and 4 goals.
    /// The prompt currently sits around 1050 characters (~265 tokens); the old
    /// un-redacted snapshot plus a 12-turn history ran several times that.
    /// </summary>
    private const int MaxPromptCharacters = 1400;

    [Fact]
    public async Task PromptStaysWithinBudget_ForAWellPopulatedAccount()
    {
        var ai = new Mock<IAiClient>();
        string? captured = null;

        ai.Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<AiTurn>, CancellationToken>((p, _, _) => captured = p)
            .ReturnsAsync(new AiCompletionResult(true, "ok", null, true));

        var dashboard = new Mock<IDashboardService>();
        dashboard.Setup(x => x.GetDashboardAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PopulatedDashboard());

        var budgets = new Mock<IBudgetService>();
        budgets.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 6).Select(i => Budget($"Budget {i} with a long free text name", 70)).ToList());

        var goals = new Mock<IFinancialGoalService>();
        goals.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 4).Select(i => Goal($"Goal {i} with a long free text name", 40, 50000)).ToList());

        var service = new FinanceChatService(
            dashboard.Object,
            budgets.Object,
            Mock.Of<IAccountService>(),
            goals.Object,
            ai.Object,
            Mock.Of<ILogger<FinanceChatService>>());

        // A long history: the point is that none of it is forwarded.
        var history = Enumerable.Range(0, 24)
            .Select(i => new ChatMessageDto(
                Guid.NewGuid(),
                i % 2 == 0 ? ChatRoles.User : ChatRoles.Assistant,
                $"Turn {i} with a reasonably long message body that would cost tokens if it were sent.",
                DateTime.Now))
            .ToList();

        await service.AskAsync(Guid.NewGuid(), "explain the difference between a stock and a bond", history);

        Assert.NotNull(captured);
        Assert.True(
            captured!.Length <= MaxPromptCharacters,
            $"Prompt was {captured.Length} chars, budget is {MaxPromptCharacters}.");
    }

    [Fact]
    public async Task PromptOmitsHistoryAndEntityNames()
    {
        var ai = new Mock<IAiClient>();
        string? captured = null;
        var turns = new List<AiTurn>();

        ai.Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<AiTurn>, CancellationToken>((p, t, _) =>
            {
                captured = p;
                turns.AddRange(t);
            })
            .ReturnsAsync(new AiCompletionResult(true, "ok", null, true));

        var dashboard = new Mock<IDashboardService>();
        dashboard.Setup(x => x.GetDashboardAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PopulatedDashboard());

        var budgets = new Mock<IBudgetService>();
        budgets.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Budget("Maria's gift fund", 96), Budget("Budi's school fees", 40) });

        var goals = new Mock<IFinancialGoalService>();
        goals.Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Goal("Secret emergency fund", 40, 50000) });

        var service = new FinanceChatService(
            dashboard.Object,
            budgets.Object,
            Mock.Of<IAccountService>(),
            goals.Object,
            ai.Object,
            Mock.Of<ILogger<FinanceChatService>>());

        var history = new List<ChatMessageDto>
        {
            new(Guid.NewGuid(), ChatRoles.User, "What's my balance?", DateTime.Now),
            new(Guid.NewGuid(), ChatRoles.Assistant, "BDO Savings: 45231.44 PHP", DateTime.Now)
        };

        await service.AskAsync(Guid.NewGuid(), "explain the difference between a stock and a bond", history);

        Assert.NotNull(captured);

        // Only the current question is forwarded.
        var turn = Assert.Single(turns);
        Assert.Equal(ChatRoles.User, turn.Role);
        Assert.Equal("explain the difference between a stock and a bond", turn.Content);

        // Nothing from the history leaks.
        Assert.DoesNotContain("BDO", captured!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("45231", captured!, StringComparison.OrdinalIgnoreCase);

        // No user-supplied entity names, and no exact figures.
        Assert.DoesNotContain("Maria", captured!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Budi", captured!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret emergency fund", captured!, StringComparison.OrdinalIgnoreCase);

        // Still useful: the rounded shape is present.
        Assert.Contains("~45,000", captured!);
        Assert.Contains("Budgets:", captured!);
        Assert.Contains("Goals:", captured!);
    }

    private static DashboardDto PopulatedDashboard() =>
        new(
            new Money(45231.44m),
            new Money(60000m),
            new Money(38450.13m),
            new Money(21549.87m),
            35.92m,
            new List<TransactionDto>(),
            Enumerable.Range(1, 5).Select(i => new CategorySpendingDto(
                new CategoryId(Guid.NewGuid()),
                $"Category {i}",
                "c",
                "#fff",
                new Money(12300 - (i * 1000)),
                32 - (i * 3))).ToList(),
            new List<MonthlyTrendDto>(),
            new List<BudgetDto>(),
            new List<FinancialGoalDto>());

    private static BudgetDto Budget(string name, decimal percentageUsed) =>
        new(
            Guid.NewGuid(),
            name,
            new Money(10000),
            new Money(10000 * (percentageUsed / 100m)),
            new Money(10000 - (10000 * (percentageUsed / 100m))),
            percentageUsed,
            DateTime.Today.AddDays(-5),
            DateTime.Today.AddDays(25),
            new CategoryId(Guid.NewGuid()),
            "Food",
            "food",
            "#FF6B6B",
            null,
            null,
            percentageUsed > 100,
            percentageUsed >= 90,
            SyncStatus.Synced,
            null,
            DateTime.Today,
            DateTime.Today,
            false,
            null);

    private static FinancialGoalDto Goal(string name, decimal progress, decimal target) =>
        new(
            Guid.NewGuid(),
            name,
            new Money(target),
            new Money(target * (progress / 100m)),
            new Money(target - (target * (progress / 100m))),
            progress,
            DateTime.Today.AddDays(200),
            DateTime.Today.AddMonths(-3),
            GoalStatus.Active,
            null,
            "target",
            "#CDF463",
            null,
            null,
            200,
            new Money(500),
            true,
            SyncStatus.Synced,
            null,
            DateTime.Today.AddMonths(-3),
            DateTime.Today,
            false);
}