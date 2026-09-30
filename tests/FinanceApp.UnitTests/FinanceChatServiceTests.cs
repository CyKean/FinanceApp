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
