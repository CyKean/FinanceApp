namespace FinanceApp.Application.Services;

public static class FinanceKnowledgeBase
{
    private static readonly (string[] Keys, string Answer)[] Topics =
    {
        (
            new[] { "50/30/20", "50-30-20", "50 30 20" },
            "The 50/30/20 rule splits your take-home pay into 50% needs (rent, food, utilities), 30% wants (dining out, subscriptions) and 20% savings or debt repayment. It is a simple starting point, not a strict law - if rent alone eats 40% of your income, adjust the ratios to fit your situation. Start by tracking where your money actually goes for a month, then steer each category toward your chosen split."),
        (
            new[] { "emergency fund", "rainy day", "unexpected expense", "safety net" },
            "An emergency fund is cash set aside only for genuine surprises like a medical bill or job loss. Aim for 3 to 6 months of essential expenses, but start with a first milestone of one month's expenses so the habit sticks. Keep it in an account that is easy to reach but not too easy to spend, and refill it whenever you use it."),
        (
            new[] { "compound interest", "compound" },
            "Compound interest means you earn interest on your interest, so money grows faster the longer it stays invested. For example, earning 8% a year, an amount roughly doubles about every 9 years without you adding anything. This is why starting early matters more than investing large amounts later."),
        (
            new[] { "apr", "annual percentage rate" },
            "APR is the yearly cost of borrowing money, expressed as a percentage. On a credit card, an APR of around 36% means a 10,000 balance costs roughly 3,600 a year in interest if you make no payments. Always compare APRs when choosing loans or cards, and prioritize paying down the highest APR debt first."),
        (
            new[] { "interest rate", "how does interest", "simple interest" },
            "Interest is the cost of borrowing money, or the reward for saving it, shown as a percentage per year. Simple interest is calculated only on the original amount, while compound interest is calculated on the amount plus previously earned interest. Higher rates and more frequent compounding grow (or cost) money faster."),
        (
            new[] { "inflation" },
            "Inflation is the gradual rise in prices, which reduces what your money can buy over time. If inflation is 4% a year, the same 1,000 buys about 4% less next year. Keeping all your cash in a no-interest account means you slowly lose purchasing power, so long-term money is usually better invested while a buffer stays liquid."),
        (
            new[] { "credit score", "credit rating", "credit report" },
            "A credit score is a number lenders use to judge how likely you are to repay on time. It is mainly influenced by payment history, how much of your available credit you use, how long you have had credit, and how often you apply for new credit. To build it: pay every bill on time, keep card balances well below the limit, and avoid opening many accounts in a short period."),
        (
            new[] { "snowball" },
            "The debt snowball method means paying the minimum on every debt, then putting all extra money toward the smallest balance first. Clearing small debts quickly gives momentum and keeps you motivated. Mathematically it may cost slightly more than the avalanche method, but adherence often matters more than the last bit of interest."),
        (
            new[] { "avalanche" },
            "The debt avalanche method means paying the minimum on every debt, then putting all extra money toward the highest-interest debt first. This minimizes total interest paid, so it is the cheapest strategy mathematically. It works best if you are comfortable waiting longer for that first full payoff."),
        (
            new[] { "debt" },
            "A solid debt plan has three parts: list every debt with its balance and rate, always pay at least the minimum on time, and attack the rest with a strategy - avalanche for lowest interest cost, snowball for quick wins. To speed things up, free up cash by trimming flexible spending and consider consolidating only when the new rate is genuinely lower. Avoid taking new debt while the old one is still outstanding."),
        (
            new[] { "index fund", "investing", "invest", "stocks", "shares", "etf" },
            "Investing means buying assets that can grow in value over years, with stocks, bonds and funds as common choices. For most beginners, a low-cost diversified index fund is a simple way to spread money across many companies at once. The key principles: only invest money you will not need for years, invest regularly regardless of market ups and downs, and keep fees low. All investing carries risk - values can fall as well as rise."),
        (
            new[] { "dollar cost averaging", "dca" },
            "Dollar-cost averaging means investing a fixed amount at regular intervals no matter what the market is doing. You buy more shares when prices are low and fewer when they are high, which smooths out your average price over time. It removes the pressure of trying to time the market and works well for automatic monthly investing."),
        (
            new[] { "retirement", "pension", "401k", "401(k)", "ira" },
            "Retirement saving works best when it is automatic and early: contribute a fixed percentage of every paycheck, increase it whenever you get a raise, and take any employer match - that match is an instant return on your money. Because the money has decades to compound, starting now with a modest amount beats starting later with a bigger one. Check the specific rules for your country's plans, as tax treatment varies."),
        (
            new[] { "savings rate" },
            "Your savings rate is the percentage of income you keep after spending: divide what you saved by what you earned. A common target is 20% of take-home pay, though any positive number you can sustain is a good start. Raise your rate gradually - automate the transfer on payday so the money is saved before you can spend it."),
        (
            new[] { "subscription", "subscriptions" },
            "Subscriptions quietly drain budgets because they are small and automatic. List every recurring charge - streaming, apps, gym, boxes - and cancel anything you have not used in the last 30 days. For the ones you keep, check whether a cheaper annual or family plan exists, then redirect the savings to a goal."),
        (
            new[] { "what is a budget", "how to budget", "budgeting tips", "how do i budget", "budget plan", "budgeting" },
            "A budget is simply a plan for where your money will go before the month starts. A practical approach: list your income, list fixed bills, assign limits to variable categories like food and transport, and review progress weekly. The best budget is one you actually keep - start with a few categories and tighten them over time rather than planning everything at once."),
        (
            new[] { "save money", "save more", "cut expenses", "reduce spending", "spend less", "tighten" },
            "To save more, attack both sides of the equation. On spending: cancel unused subscriptions, set a weekly limit for dining and impulse buys, compare prices on recurring purchases, and automate a transfer to savings on payday. On income: consider negotiating your rate, selling what you do not use, or adding a small side earner. Track the result monthly so the progress keeps you motivated."),
        (
            new[] { "side hustle", "extra income", "second income" },
            "Extra income accelerates any financial goal because there is a limit to how much you can cut, but no limit to what you can earn. Good starting points are selling unused items, freelancing a skill you already use at work, or a small local service. Treat it as temporary income: park the earnings straight into savings or debt so lifestyle inflation does not swallow them."),
        (
            new[] { "liquidity", "liquid" },
            "Liquidity is how quickly an asset can be converted to cash without losing value. Cash and savings accounts are highly liquid; property and long-term investments are not. Keep your emergency fund in a liquid place so you are never forced to sell investments at a bad time."),
        (
            new[] { "net worth" },
            "Net worth is everything you own minus everything you owe: cash, savings and assets minus debts. It is the clearest single number for your financial health because it grows whether you earn more, spend less, or repay debt. Calculate it once a month and watch the trend rather than any single month's figure."),
        (
            new[] { "taxes", " tax" },
            "Taxes are charges on income, purchases or gains that fund public services. The details vary a lot by country, but two habits apply everywhere: keep records of deductible expenses, and set aside a slice of every payment if you are self-employed. When a decision has real money at stake, check your local rules or ask a qualified tax professional."),
        (
            new[] { "payday", "paycheck", "every two weeks" },
            "A payday routine keeps the month on track: the day you are paid, move your savings and bill money first, then plan the rest. If you are paid bi-weekly, map bills to the paychecks that cover them instead of the calendar month, which smooths out uneven months. Automating all of this removes willpower from the equation."),
        (
            new[] { "should i buy", "worth buying", "buying advice" },
            "Before a non-essential purchase, give it a 48-hour cooling-off period and ask three questions: does it solve a real problem, can I pay for it without borrowing, and is there a cheaper option that does the same job? If the answer to the second question is no, the answer to the purchase is usually no too. For large buys, compare the total cost - including fees and running costs - not just the sticker price."),
        (
            new[] { "financial goals", "goal setting" },
            "Good financial goals are specific, dated and automatic: instead of 'save more', aim for 'save 30,000 for an emergency fund by December'. Break the target into a monthly amount, schedule the transfer on payday, and review progress monthly. Keep one short-term goal and one long-term goal at a time so your money has a clear job.")
    };

    public static string? Match(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return null;

        var normalized = question.Trim().ToLowerInvariant();

        foreach (var (keys, answer) in Topics)
        {
            foreach (var key in keys)
            {
                if (normalized.Contains(key, StringComparison.Ordinal))
                    return answer;
            }
        }

        return null;
    }
}
