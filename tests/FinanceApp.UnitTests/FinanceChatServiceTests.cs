using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Application.Services;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceApp.UnitTests;

public class FinanceChatServiceTests
{
    private readonly Mock<IDashboardService> _dashboardService = new();
    private readonly Mock<IBudgetService> _budgetService = new();
    private readonly Mock<IAccountService> _accountService = new();
    private readonly Mock<IFinancialGoalService> _goalService = new();
    private readonly Mock<IAiClient> _aiClient = new();
    private readonly FinanceChatService _service;

    public FinanceChatServiceTests()
    {
        _service = new FinanceChatService(
            _dashboardService.Object,
            _budgetService.Object,
            _accountService.Object,
            _goalService.Object,
            _aiClient.Object,
            Mock.Of<ILogger<FinanceChatService>>());
    }

    [Fact]
    public async Task AskAsync_ReturnsKnowledgeBaseAnswer_ForGeneralFinanceQuestion()
    {
        var reply = await _service.GetReplyAsync("What is APR?");

        Assert.Equal(ChatReplySource.KnowledgeBase, reply.Source);
        Assert.False(string.IsNullOrWhiteSpace(reply.Content));
        _aiClient.Verify(
            x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AskAsync_AnswersFromAccountData_ForBalanceQuestion()
    {
        _accountService
            .Setup(x => x.GetTotalBalanceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Money(15000m));
        _accountService
            .Setup(x => x.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountDto>
            {
                new(Guid.NewGuid(), "Cash", AccountType.Cash, new Money(15000m), null, null, null, true, 0,
                    DateTime.Today, DateTime.Today, false)
            });

        var reply = await _service.GetReplyAsync("What's my balance?");

        Assert.Equal(ChatReplySource.PersonalData, reply.Source);
        Assert.Contains("15,000", reply.Content);
        _aiClient.Verify(
            x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AskAsync_UsesAiClient_ForOpenEndedQuestion()
    {
        _aiClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletionResult(true, "Here is a thought about economics.", null, true));

        var reply = await _service.GetReplyAsync("Tell me something interesting about economics");

        Assert.Equal(ChatReplySource.Assistant, reply.Source);
        Assert.Equal("Here is a thought about economics.", reply.Content);
    }

    [Fact]
    public async Task AskAsync_FallsBack_WhenAiIsNotConfigured()
    {
        _aiClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletionResult(false, null, "No API key configured", false));

        var reply = await _service.GetReplyAsync("What's the meaning of fiscal policy?");

        Assert.Equal(ChatReplySource.Fallback, reply.Source);
        Assert.False(string.IsNullOrWhiteSpace(reply.Content));
    }

    [Fact]
    public async Task AskAsync_AnswersAffordability_WithDashboardNumbers()
    {
        _dashboardService
            .Setup(x => x.GetDashboardAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDashboard(balance: 10000m, income: 5000m, expense: 2000m));

        var reply = await _service.GetReplyAsync("Can I afford 1,500 for dinner?");

        Assert.Equal(ChatReplySource.PersonalData, reply.Source);
        Assert.Contains("1,500", reply.Content);
        Assert.Contains("10,000", reply.Content);
    }

    [Fact]
    public async Task AskAsync_SurfacesTheAiErrorReason()
    {
        // Every provider failure used to collapse into the same sentence, so a
        // rejected key, a rate limit and a wrong base URL were indistinguishable.
        _aiClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletionResult(false, null, "Your API key was rejected.", true));

        var reply = await _service.GetReplyAsync("Tell me something interesting about economics");

        Assert.Equal(ChatReplySource.Fallback, reply.Source);
        Assert.Contains("Your API key was rejected.", reply.Content);
        Assert.Contains("Settings - AI Assistant", reply.Content);
    }

    [Fact]
    public async Task AskAsync_DoesNotClaimOffline_WhenTheProviderSimplyFailed()
    {
        _aiClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletionResult(false, null, "Rate limit or quota reached.", true));

        var reply = await _service.GetReplyAsync("Tell me something interesting about economics");

        Assert.Contains("Rate limit or quota reached.", reply.Content);
        Assert.DoesNotContain("You appear to be offline", reply.Content);
    }

    [Fact]
    public async Task AskAsync_FallsBackWithGuidance_WhenNoErrorReasonGiven()
    {
        _aiClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletionResult(false, null, null, true));

        var reply = await _service.GetReplyAsync("Tell me something interesting about economics");

        Assert.Equal(ChatReplySource.Fallback, reply.Source);
        Assert.False(string.IsNullOrWhiteSpace(reply.Content));
    }

    [Fact]
    public async Task AskAsync_ReportsCancellationSeparately_FromFailure()
    {
        _aiClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var reply = await _service.GetReplyAsync("Tell me something interesting about economics");

        Assert.Contains("cancelled", reply.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskAsync_KeepsAnsweringLocally_WhenPersonalLookupThrows()
    {
        _accountService
            .Setup(x => x.GetTotalBalanceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database gone"));

        var reply = await _service.GetReplyAsync("What is APR?");

        Assert.Equal(ChatReplySource.KnowledgeBase, reply.Source);
    }

    [Fact]
    public async Task AskAsync_DoesNotForwardConversationHistory()
    {
        _aiClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletionResult(true, "ok", null, true));

        // History containing real account names and per-transaction detail from
        // the on-device handlers. None of it may reach the provider.
        var history = new List<ChatMessageDto>
        {
            new(Guid.NewGuid(), ChatRoles.User, "What's my balance?", DateTime.Now),
            new(Guid.NewGuid(), ChatRoles.Assistant, "BDO Savings: 45,200.00 PHP", DateTime.Now),
            new(Guid.NewGuid(), ChatRoles.User, "Show recent transactions", DateTime.Now),
            new(Guid.NewGuid(), ChatRoles.Assistant, "Oct 03: -850.00 PHP - Dining", DateTime.Now)
        };

        await _service.AskAsync(Guid.NewGuid(), "why do I overspend on food?", history);

        _aiClient.Verify(
            x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var sentTurns = CapturedTurns();
        var turn = Assert.Single(sentTurns);
        Assert.Equal(ChatRoles.User, turn.Role);
        Assert.Equal("why do I overspend on food?", turn.Content);

        Assert.DoesNotContain("BDO", sentTurns[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Dining", sentTurns[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskAsync_SnapshotRedactsExactAmountsAndEntityNames()
    {
        _aiClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletionResult(true, "ok", null, true));

        _dashboardService
            .Setup(x => x.GetDashboardAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDashboard(balance: 45231.44m, income: 60000m, expense: 38450.13m));

        _budgetService
            .Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Budget("Maria's gift budget", 75m),
                Budget("Budi's school fees", 40m)
            });

        _goalService
            .Setup(x => x.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Goal("Emergency Fund", 40m, 50000m, 200) });

        await _service.AskAsync(Guid.NewGuid(), "explain the difference between a stock and a bond", Array.Empty<ChatMessageDto>());

        // Guard: if local routing ever starts answering this, the assertions
        // below would pass vacuously on an empty prompt.
        _aiClient.Verify(
            x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var prompt = CapturedPrompt();

        // Exact figures must not appear.
        Assert.DoesNotContain("45231", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("38450", prompt, StringComparison.OrdinalIgnoreCase);

        // User-supplied entity names must not appear.
        Assert.DoesNotContain("Maria", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Budi", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Emergency Fund", prompt, StringComparison.OrdinalIgnoreCase);

        // The rounded shape must still be usable.
        Assert.Contains("~45,000", prompt);
        Assert.Contains("Budgets:", prompt);
        Assert.Contains("Goals:", prompt);
    }

    [Fact]
    public async Task AskAsync_TellsTheModelFiguresAreApproximate()
    {
        _aiClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletionResult(true, "ok", null, true));

        await _service.AskAsync(Guid.NewGuid(), "explain the difference between a stock and a bond", Array.Empty<ChatMessageDto>());

        var prompt = CapturedPrompt();

        Assert.Contains("rounded approximations", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never state them as exact", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskAsync_DoesNotThrow_WhenSnapshotUnavailable()
    {
        _aiClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AiTurn>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletionResult(true, "ok", null, true));

        _dashboardService
            .Setup(x => x.GetDashboardAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database gone"));

        var reply = await _service.GetReplyAsync("tell me something interesting about economics");

        Assert.Equal(ChatReplySource.Assistant, reply.Source);
        Assert.Contains("unavailable", CapturedPrompt(), StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<AiTurn> CapturedTurns()
    {
        _aiClient.Verify(x => x.CompleteAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<AiTurn>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        foreach (var invocation in _aiClient.Invocations)
        {
            if (invocation.Method.Name == nameof(IAiClient.CompleteAsync))
                return (IReadOnlyList<AiTurn>)invocation.Arguments[1]!;
        }

        throw new InvalidOperationException("CompleteAsync was never called");
    }

    private string CapturedPrompt() => (string)CapturedTurnsInvocationPrompt();

    private object CapturedTurnsInvocationPrompt()
    {
        foreach (var invocation in _aiClient.Invocations)
        {
            if (invocation.Method.Name == nameof(IAiClient.CompleteAsync))
                return invocation.Arguments[0]!;
        }

        throw new InvalidOperationException("CompleteAsync was never called");
    }

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
            false,
            false,
            SyncStatus.Synced,
            null,
            DateTime.Today,
            DateTime.Today,
            false,
            null);

    private static FinancialGoalDto Goal(string name, decimal progress, decimal target, int daysRemaining) =>
        new(
            Guid.NewGuid(),
            name,
            new Money(target),
            new Money(target * (progress / 100m)),
            new Money(target - (target * (progress / 100m))),
            progress,
            DateTime.Today.AddDays(daysRemaining),
            DateTime.Today.AddMonths(-3),
            GoalStatus.Active,
            null,
            "target",
            "#CDF463",
            null,
            null,
            daysRemaining,
            new Money(500),
            true,
            SyncStatus.Synced,
            null,
            DateTime.Today.AddMonths(-3),
            DateTime.Today,
            false);

    private static DashboardDto CreateDashboard(decimal balance, decimal income, decimal expense)
    {
        var totalBalance = new Money(balance);
        var totalIncome = new Money(income);
        var totalExpense = new Money(expense);

        return new DashboardDto(
            totalBalance,
            totalIncome,
            totalExpense,
            totalIncome.Subtract(totalExpense),
            0m,
            new List<TransactionDto>(),
            new List<CategorySpendingDto>(),
            new List<MonthlyTrendDto>(),
            new List<BudgetDto>(),
            new List<FinancialGoalDto>());
    }
}

internal static class FinanceChatServiceTestExtensions
{
    public static Task<ChatReplyDto> GetReplyAsync(this IFinanceChatService service, string question) =>
        service.AskAsync(Guid.NewGuid(), question, Array.Empty<ChatMessageDto>());
}
