namespace FinanceApp.Application.Services;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FinanceApp.Application.DTOs;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

public class FinanceChatService : IFinanceChatService
{
    private const int AnalysisMonths = 3;
    private const int HistoryTurnLimit = 12;

    private static readonly string[] SpendPhrases =
    {
        "spent on", "spend on", "spending on", "paid for", "spent for", "cost me"
    };

    private static readonly string[] AmountSuffixes = { "k", "m", "b" };

    private readonly IDashboardService _dashboardService;
    private readonly IBudgetService _budgetService;
    private readonly IAccountService _accountService;
    private readonly IFinancialGoalService _goalService;
    private readonly IAiClient _aiClient;
    private readonly ILogger<FinanceChatService> _logger;

    public FinanceChatService(
        IDashboardService dashboardService,
        IBudgetService budgetService,
        IAccountService accountService,
        IFinancialGoalService goalService,
        IAiClient aiClient,
        ILogger<FinanceChatService> logger)
    {
        _dashboardService = dashboardService;
        _budgetService = budgetService;
        _accountService = accountService;
        _goalService = goalService;
        _aiClient = aiClient;
        _logger = logger;
    }

    public async Task<ChatReplyDto> AskAsync(Guid userId, string question, IReadOnlyList<ChatMessageDto> history, CancellationToken cancellationToken = default)
    {
        var trimmed = question?.Trim() ?? string.Empty;
        var lower = trimmed.ToLowerInvariant();

        if (IsGeneralQuestion(lower) && !HasPersonalMarker(lower))
        {
            var generalAnswer = FinanceKnowledgeBase.Match(lower);
            if (generalAnswer is not null)
                return new ChatReplyDto(generalAnswer, ChatReplySource.KnowledgeBase);
        }

        try
        {
            var personalAnswer = await TryAnswerPersonalAsync(userId, lower, cancellationToken);
            if (personalAnswer is not null)
                return personalAnswer;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Personal data lookup failed for user {UserId}", userId);
        }

        var knowledgeAnswer = FinanceKnowledgeBase.Match(lower);
        if (knowledgeAnswer is not null)
            return new ChatReplyDto(knowledgeAnswer, ChatReplySource.KnowledgeBase);

        return await AskAssistantAsync(userId, trimmed, history, cancellationToken);
    }

    private async Task<ChatReplyDto?> TryAnswerPersonalAsync(Guid userId, string lower, CancellationToken cancellationToken)
    {
        if (IsAffordability(lower))
            return await AnswerAffordabilityAsync(userId, lower, cancellationToken);

        if (IsBudgetStatus(lower))
            return await AnswerBudgetsAsync(userId, cancellationToken);

        var categoryAnswer = await TryAnswerCategorySpendAsync(userId, lower, cancellationToken);
        if (categoryAnswer is not null)
            return categoryAnswer;

        if (IsSavings(lower))
            return await AnswerSavingsAsync(userId, lower, cancellationToken);

        if (IsBalance(lower))
            return await AnswerBalanceAsync(userId, cancellationToken);

        if (IsMonthSummary(lower))
            return await AnswerMonthSummaryAsync(userId, cancellationToken);

        if (IsGoals(lower))
            return await AnswerGoalsAsync(userId, cancellationToken);

        if (IsRecent(lower))
            return await AnswerRecentAsync(userId, cancellationToken);

        return null;
    }

    private static bool IsAffordability(string lower) =>
        ContainsAny(lower, "can i afford", "can we afford", "affordable", "can i buy", "enough to buy");

    private static bool IsBudgetStatus(string lower) =>
        lower.Contains("budget", StringComparison.Ordinal) &&
        ContainsAny(lower, "my budget", "budgets", "left", "remain", "overspent", "over budget", "on track", "status", "how am i", "how are", "doing");

    private static bool IsSavings(string lower) =>
        ContainsAny(lower, "save", "saving", "savings", "set aside", "put aside");

    private static bool IsBalance(string lower) =>
        ContainsAny(lower, "balance", "how much do i have", "how much money do i have", "net worth", "how much have i got");

    private static bool IsMonthSummary(string lower) =>
        ContainsAny(lower, "this month", "how much did i spend", "how much have i spent", "spent so far", "how much did i spend", "income", "expense", "cash flow", "how am i doing", "how are my finances");

    private static bool IsGoals(string lower) =>
        lower.Contains("goal", StringComparison.Ordinal);

    private static bool IsRecent(string lower) =>
        ContainsAny(lower, "recent", "last transaction", "latest transaction", "last few", "last 5");

    private static bool IsGeneralQuestion(string lower) =>
        ContainsAny(lower, "what is", "what's", "what are", "what does", "how do i", "how does", "how to", "explain ", "define ", "difference between", "why do", "why is", "should i");

    private static bool HasPersonalMarker(string lower) =>
        ContainsAny(lower, "my ", "i have", "did i", "have i", "i spent", "i earned", "we ", "our ", "me ");

    private static bool ContainsAny(string value, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (value.Contains(candidate, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private async Task<ChatReplyDto> AnswerAffordabilityAsync(Guid userId, string lower, CancellationToken cancellationToken)
    {
        var dashboard = await _dashboardService.GetDashboardAsync(userId, cancellationToken);
        var amount = ExtractAmount(lower, dashboard.TotalBalance.Currency);

        if (amount is null)
        {
            return new ChatReplyDto(
                "Tell me the price and I can check it against your balance - for example 'Can I afford 1,500 for dinner?'.",
                ChatReplySource.PersonalData);
        }

        var balance = dashboard.TotalBalance;
        var net = dashboard.NetAmount;
        var builder = new StringBuilder();
        builder.AppendLine($"That would cost {amount}.");
        builder.AppendLine($"Your current balance is {balance} across your accounts.");
        builder.AppendLine($"This month you earned {dashboard.TotalIncome} and spent {dashboard.TotalExpense}.");

        if (amount > balance)
        {
            builder.Append($"You cannot afford it right now - it is {amount - balance} more than your balance.");
        }
        else if (net.Amount > 0 && amount > net)
        {
            builder.Append("It fits in your balance, but it is more than this month's surplus, so it would slow your savings - consider waiting or spending a little less.");
        }
        else if (net.Amount <= 0)
        {
            builder.Append("Your balance covers it, but you are spending more than you earn this month - I would hold off unless it is essential.");
        }
        else
        {
            builder.Append($"Yes - after this purchase you would still have {balance - amount} left and stay positive this month.");
        }

        return new ChatReplyDto(builder.ToString().Trim(), ChatReplySource.PersonalData);
    }

    private async Task<ChatReplyDto> AnswerBudgetsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var budgets = await _budgetService.GetActiveAsync(userId, DateTime.Today, cancellationToken);

        if (budgets.Count == 0)
        {
            return new ChatReplyDto(
                "You have no active budgets right now. Open Budgets and tap 'AI suggestions' if you want ideas based on your spending.",
                ChatReplySource.PersonalData);
        }

        var builder = new StringBuilder();
        builder.AppendLine($"You have {budgets.Count} active budget{(budgets.Count == 1 ? "" : "s")}:");
        var overCount = 0;
        var nearCount = 0;

        foreach (var budget in budgets.OrderByDescending(b => b.PercentageUsed))
        {
            if (budget.IsOverBudget) overCount++;
            else if (budget.IsNearLimit) nearCount++;

            var state = budget.IsOverBudget
                ? $"OVER by {budget.SpentAmount - budget.Amount}"
                : $"{budget.RemainingAmount} left";
            builder.AppendLine($"• {budget.Name}: {budget.SpentAmount} of {budget.Amount} ({budget.PercentageUsed:0.#}%) - {state}");
        }

        if (overCount > 0)
            builder.Append($"{overCount} budget{(overCount == 1 ? " is" : "s are")} over the limit - trim that category or raise the budget deliberately.");
        else if (nearCount > 0)
            builder.Append($"{nearCount} budget{(nearCount == 1 ? " is" : "s are")} at 80% or more - watch them for the rest of the period.");
        else
            builder.Append("Everything is within limits - nice work keeping it on track.");

        return new ChatReplyDto(builder.ToString().Trim(), ChatReplySource.PersonalData);
    }

    private async Task<ChatReplyDto?> TryAnswerCategorySpendAsync(Guid userId, string lower, CancellationToken cancellationToken)
    {
        var phraseIndex = -1;
        var phraseLength = 0;
        foreach (var phrase in SpendPhrases)
        {
            var index = lower.IndexOf(phrase, StringComparison.Ordinal);
            if (index >= 0)
            {
                phraseIndex = index;
                phraseLength = phrase.Length;
                break;
            }
        }

        if (phraseIndex < 0)
            return null;

        var tail = CleanQuestionTail(lower[(phraseIndex + phraseLength)..]);
        if (tail.Length == 0)
            return null;

        var dashboard = await _dashboardService.GetDashboardAsync(userId, cancellationToken);
        var analytics = await _dashboardService.GetAnalyticsAsync(userId, AnalysisMonths, cancellationToken);

        var thisMonthMatch = dashboard.SpendingByCategory.FirstOrDefault(c => CategoryMatches(c.CategoryName, tail));
        if (thisMonthMatch is not null)
        {
            return new ChatReplyDto(
                $"You spent {thisMonthMatch.Amount} on {thisMonthMatch.CategoryName} this month ({thisMonthMatch.Percentage:0.#}% of your spending).",
                ChatReplySource.PersonalData);
        }

        var quarterMatch = analytics.CategoryBreakdown.FirstOrDefault(c => CategoryMatches(c.CategoryName, tail));
        if (quarterMatch is not null)
        {
            var monthly = quarterMatch.Amount.Divide(AnalysisMonths);
            return new ChatReplyDto(
                $"Not this month yet, but over the last {AnalysisMonths} months you averaged {monthly} a month on {quarterMatch.CategoryName}.",
                ChatReplySource.PersonalData);
        }

        var topCategories = string.Join(", ",
            dashboard.SpendingByCategory.Take(3).Select(c => $"{c.CategoryName} ({c.Amount})"));
        var knownLine = topCategories.Length > 0
            ? $"Your biggest categories this month are {topCategories}."
            : "You have not recorded any spending this month.";

        return new ChatReplyDto(
            $"I could not find spending for '{tail}' in your data. {knownLine}",
            ChatReplySource.PersonalData);
    }

    private async Task<ChatReplyDto> AnswerSavingsAsync(Guid userId, string lower, CancellationToken cancellationToken)
    {
        var dashboard = await _dashboardService.GetDashboardAsync(userId, cancellationToken);
        var monthlySavings = dashboard.NetAmount;
        var target = ExtractAmount(lower, dashboard.TotalIncome.Currency);
        var builder = new StringBuilder();

        if (target is not null)
        {
            if (monthlySavings.Amount <= 0)
            {
                builder.AppendLine($"Saving {target} would take a while: you are currently spending {dashboard.TotalExpense - dashboard.TotalIncome} more than you earn this month.");
                builder.Append("First step: free up any amount each month, even a small one, and automate it on payday.");
            }
            else
            {
                var months = (int)Math.Ceiling(target.Amount / monthlySavings.Amount);
                builder.AppendLine($"At your current pace of {monthlySavings} saved per month, reaching {target} takes about {months} month{(months == 1 ? "" : "s")}.");
                builder.Append(months > 12
                    ? "To get there faster, combine a higher savings rate with a one-off cut in flexible spending."
                    : "Keep the transfers automatic and you will get there without thinking about it.");
            }

            return new ChatReplyDto(builder.ToString().Trim(), ChatReplySource.PersonalData);
        }

        builder.AppendLine($"This month you saved {monthlySavings} - a savings rate of {dashboard.SavingsRate:0.#}% of your income.");

        if (dashboard.TotalIncome.Amount > 0 && dashboard.SavingsRate < 20)
        {
            var suggested = dashboard.TotalIncome.Multiply(0.20m);
            builder.Append($"To reach a 20% savings rate you would need {suggested} set aside - a gap of {suggested - MoneyMax(monthlySavings, dashboard.TotalIncome.Currency)}.");
        }
        else if (dashboard.SavingsRate >= 20)
        {
            builder.Append("That is above the common 20% target - consider putting the extra into your goals or long-term investments.");
        }
        else
        {
            builder.Append("Aim to increase it a little each month rather than overhauling everything at once.");
        }

        return new ChatReplyDto(builder.ToString().Trim(), ChatReplySource.PersonalData);
    }

    private async Task<ChatReplyDto> AnswerBalanceAsync(Guid userId, CancellationToken cancellationToken)
    {
        var total = await _accountService.GetTotalBalanceAsync(userId, cancellationToken);
        var accounts = await _accountService.GetAllAsync(userId, cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine($"Your total balance is {total}.");

        foreach (var account in accounts.OrderByDescending(a => a.Balance.Amount).Take(4))
            builder.AppendLine($"• {account.Name}: {account.Balance}");

        if (accounts.Count > 4)
            builder.AppendLine($"…and {accounts.Count - 4} more account{(accounts.Count - 4 == 1 ? "" : "s")}.");

        return new ChatReplyDto(builder.ToString().Trim(), ChatReplySource.PersonalData);
    }

    private async Task<ChatReplyDto> AnswerMonthSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var dashboard = await _dashboardService.GetDashboardAsync(userId, cancellationToken);
        var analytics = await _dashboardService.GetAnalyticsAsync(userId, 2, cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine($"This month: income {dashboard.TotalIncome}, spending {dashboard.TotalExpense}, net {dashboard.NetAmount}.");
        builder.AppendLine($"Your savings rate is {dashboard.SavingsRate:0.#}%.");

        var trends = analytics.SpendingTrends;
        if (trends.Count >= 2)
        {
            var previous = trends[trends.Count - 2].Expense.Amount;
            var current = trends[trends.Count - 1].Expense.Amount;
            if (previous > 0)
            {
                var change = Math.Round((current - previous) / previous * 100, 1);
                builder.AppendLine(change switch
                {
                    > 0 => $"Spending is up {change}% versus last month.",
                    < 0 => $"Spending is down {Math.Abs(change)}% versus last month.",
                    _ => "Spending is level with last month."
                });
            }
        }

        var top = dashboard.SpendingByCategory.FirstOrDefault();
        if (top is not null)
            builder.Append($"Biggest category: {top.CategoryName} at {top.Amount} ({top.Percentage:0.#}%).");

        return new ChatReplyDto(builder.ToString().Trim(), ChatReplySource.PersonalData);
    }

    private async Task<ChatReplyDto> AnswerGoalsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var goals = await _goalService.GetActiveAsync(userId, cancellationToken);

        if (goals.Count == 0)
            return new ChatReplyDto(
                "You have no active savings goals yet. Create one from the Goals page and I can track it with you.",
                ChatReplySource.PersonalData);

        var builder = new StringBuilder();
        builder.AppendLine($"You have {goals.Count} active goal{(goals.Count == 1 ? "" : "s")}:");
        foreach (var goal in goals.OrderByDescending(g => g.ProgressPercentage))
        {
            builder.AppendLine($"• {goal.Name}: {goal.ProgressPercentage:0.#}% ({goal.CurrentAmount} of {goal.TargetAmount}), {(goal.IsOnTrack ? "on track" : "behind schedule")}, {goal.DaysRemaining} days left");
        }

        return new ChatReplyDto(builder.ToString().Trim(), ChatReplySource.PersonalData);
    }

    private async Task<ChatReplyDto> AnswerRecentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var dashboard = await _dashboardService.GetDashboardAsync(userId, cancellationToken);

        if (dashboard.RecentTransactions.Count == 0)
            return new ChatReplyDto("You have no recent transactions recorded.", ChatReplySource.PersonalData);

        var builder = new StringBuilder();
        builder.AppendLine("Your latest transactions:");
        foreach (var transaction in dashboard.RecentTransactions.Take(5))
        {
            var sign = transaction.Type == FinanceApp.Domain.Enums.TransactionType.Income ? "+" : "-";
            builder.AppendLine($"• {transaction.Date:MMM dd}: {sign}{transaction.Amount} - {transaction.CategoryName}");
        }

        return new ChatReplyDto(builder.ToString().Trim(), ChatReplySource.PersonalData);
    }

    private async Task<ChatReplyDto> AskAssistantAsync(Guid userId, string question, IReadOnlyList<ChatMessageDto> history, CancellationToken cancellationToken)
    {
        var snapshot = await BuildSnapshotAsync(userId, cancellationToken);
        var systemPrompt =
            "You are the built-in finance assistant for FinanceApp, a personal budgeting app. " +
            "Answer in at most 5 short sentences using plain text - no markdown, no tables, no headers. " +
            "Use the user's financial snapshot below whenever it is relevant, and never invent numbers that are not in it. " +
            "Be practical and friendly; for major financial decisions suggest speaking to a licensed professional. " +
            "If the snapshot does not contain the answer, say what information you would need.\n\n" +
            "Financial snapshot:\n" + snapshot;

        var turns = new List<AiTurn>();
        foreach (var message in history.Skip(Math.Max(0, history.Count - HistoryTurnLimit)))
        {
            if (string.IsNullOrWhiteSpace(message.Content)) continue;
            turns.Add(new AiTurn(message.IsUser ? ChatRoles.User : ChatRoles.Assistant, message.Content));
        }

        if (!turns.Any(t => string.Equals(t.Content, question, StringComparison.Ordinal)))
            turns.Add(new AiTurn(ChatRoles.User, question));

        try
        {
            var result = await _aiClient.CompleteAsync(systemPrompt, turns, cancellationToken);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Content))
                return new ChatReplyDto(result.Content.Trim(), ChatReplySource.Assistant);

            if (!result.IsConfigured)
            {
                return new ChatReplyDto(
                    "I could not find a local answer for that, and cloud AI is not switched on. " +
                    "Add an API key in Settings - AI Assistant to enable full answers, or ask me things like: " +
                    "'How am I doing this month?', 'Can I afford 1,500 for dinner?', 'What is the 50/30/20 rule?'",
                    ChatReplySource.Fallback);
            }

            _logger.LogWarning("AI request failed: {Error}", result.Error);

            var offlineHint = result.Error is not null && result.Error.Contains("offline", StringComparison.OrdinalIgnoreCase)
                ? " You appear to be offline."
                : string.Empty;

            return new ChatReplyDto(
                $"I could not reach the AI service right now.{offlineHint} Try again in a moment, or ask me about your budgets, spending, savings, or a general finance topic.",
                ChatReplySource.Fallback);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI assistant call failed for user {UserId}", userId);
            return new ChatReplyDto(
                "Something went wrong while contacting the AI service. Please try again in a moment.",
                ChatReplySource.Fallback);
        }
    }

    private async Task<string> BuildSnapshotAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var dashboard = await _dashboardService.GetDashboardAsync(userId, cancellationToken);
            var budgets = await _budgetService.GetActiveAsync(userId, DateTime.Today, cancellationToken);
            var goals = await _goalService.GetActiveAsync(userId, cancellationToken);

            var builder = new StringBuilder();
            builder.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd}");
            builder.AppendLine($"Total balance: {dashboard.TotalBalance}");
            builder.AppendLine($"This month income: {dashboard.TotalIncome}, spending: {dashboard.TotalExpense}, net: {dashboard.NetAmount}, savings rate: {dashboard.SavingsRate}%");

            if (dashboard.SpendingByCategory.Count > 0)
            {
                builder.AppendLine("Top spending categories this month: " + string.Join(", ",
                    dashboard.SpendingByCategory.Take(5).Select(c => $"{c.CategoryName} {c.Amount} ({c.Percentage:0.#}%)")));
            }

            if (budgets.Count > 0)
            {
                builder.AppendLine("Active budgets: " + string.Join(", ",
                    budgets.Take(6).Select(b => $"{b.Name} {b.SpentAmount}/{b.Amount} ({b.PercentageUsed:0.#}%)")));
            }

            if (goals.Count > 0)
            {
                builder.AppendLine("Active goals: " + string.Join(", ",
                    goals.Take(4).Select(g => $"{g.Name} {g.ProgressPercentage:0.#}% (target {g.TargetAmount})")));
            }

            return builder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not build financial snapshot for user {UserId}", userId);
            return "Financial data is unavailable at the moment.";
        }
    }

    private static string CleanQuestionTail(string tail)
    {
        var cleaned = tail.Trim().Trim('?', '.', ',', '!', ';', ':');
        string[] trailingPhrases =
        {
            "this month", "last month", "so far", "in total", "per month", "each month",
            "this year", "on average", "usually", "at all"
        };

        foreach (var phrase in trailingPhrases)
        {
            if (cleaned.EndsWith(" " + phrase, StringComparison.Ordinal))
                cleaned = cleaned[..^(phrase.Length + 1)].Trim();
        }

        return cleaned.Trim();
    }

    private static bool CategoryMatches(string categoryName, string tail)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return false;

        var name = categoryName.Trim().ToLowerInvariant();
        return tail.Contains(name, StringComparison.Ordinal) || name.Contains(tail, StringComparison.Ordinal);
    }

    private static Money? ExtractAmount(string text, string currency)
    {
        var matches = Regex.Matches(text, @"(\d[\d,]*(?:\.\d+)?)\s*([kmb])?\b");
        decimal best = 0;

        foreach (Match match in matches)
        {
            var raw = match.Groups[1].Value.Replace(",", "");
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                continue;

            var suffix = match.Groups[2].Value.ToLowerInvariant();
            if (suffix == "k") value *= 1_000m;
            else if (suffix == "m") value *= 1_000_000m;
            else if (suffix == "b") value *= 1_000_000_000m;

            if (value > best) best = value;
        }

        return best > 0 ? new Money(best, currency) : null;
    }

    private static Money MoneyMax(Money amount, string currency) =>
        amount.Amount > 0 ? amount : Money.Zero(currency);
}
