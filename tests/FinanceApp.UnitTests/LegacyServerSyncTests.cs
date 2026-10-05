namespace FinanceApp.UnitTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using Xunit;

/// <summary>
/// A Supabase project that has never run migration 0003: its accounts table has
/// no <c>initial_balance_amount</c> column at all.
/// <para>
/// The select the client sends does not name columns, so a missing one does not
/// fail the pull - it simply arrives unset. Read as zero it would give every
/// account an opening balance of nothing, and the recalculation at the end of
/// the pull would write that over the balance the user was looking at: the
/// reported bug, by another route, and once for every account in the app.
/// </para>
/// </summary>
public class LegacyServerSyncTests : IDisposable
{
    private readonly PostgrestStub _cloud = new();
    private readonly TestDevice _phone;
    private readonly TestDevice _device;

    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public LegacyServerSyncTests()
    {
        _phone = new TestDevice(_cloud, UserId);
        _device = new TestDevice(_cloud, UserId);
    }

    [Fact]
    public async Task AnAccountFromAProjectWithoutAnOpeningBalanceColumn_KeepsTheBalanceItShowed()
    {
        await MakeTheServerLookUnmigratedAsync();

        await _device.Sync().SyncAsync(UserId);

        // 200 opened, 100 spent. Without the column the opening comes back as
        // zero and the account reads -100 instead.
        Assert.Equal(100m, Assert.Single(_device.ReadAccounts()).Balance);
    }

    [Fact]
    public async Task PushingAnAccountAfterThat_DropsTheColumnTheTableDoesNotHave()
    {
        await MakeTheServerLookUnmigratedAsync();

        // The pull is what notices; the push afterwards has to keep to the
        // columns the table has, or every account update fails on the first one
        // and the sync retries it for the rest of the process's life.
        var transport = _device.Transport();
        await transport.PullAsync(UserId);

        var account = Assert.Single(await _device.Accounts.GetByUserIdAsync(UserId));
        await transport.SyncAccountAsync(account, SyncOperationType.Update);

        var posted = _cloud.Rows("accounts")[account.Id.ToString()];
        Assert.Contains("Cash", posted);
        Assert.DoesNotContain("initial_balance_amount", posted);
    }

    /// <summary>
    /// Cash opened at 200 and was pushed with that figure, then a 100 expense
    /// was recorded against it - after which the account rows this old build
    /// pushed stop carrying anything but the opening figure it first sent, and
    /// the opening column does not exist yet.
    /// </summary>
    private async Task MakeTheServerLookUnmigratedAsync()
    {
        var cash = new Account("Cash", AccountType.Cash, new Money(200), UserId, isDefault: true);
        await _phone.Accounts.AddAsync(cash);

        var food = new Category("Food", CategoryType.Expense, UserId, isSystem: true);
        await _phone.Categories.AddAsync(food);
        await _phone.Work.SaveChangesAsync();

        await _phone.Transactions.AddAsync(new Transaction(
            TransactionType.Expense, new Money(100), DateTime.Today,
            new AccountId(cash.Id), (CategoryId)food.Id, UserId));
        await _phone.Work.SaveChangesAsync();

        await _phone.Balances.RecalculateAsync(cash.Id);
        await _phone.Sync().SyncAsync(UserId);

        _cloud.Edit(
            "accounts",
            _cloud.Rows("accounts").Keys.Single(),
            ("initial_balance_amount", null),
            ("balance_amount", 200m));
    }

    public void Dispose()
    {
        _device.Dispose();
        _phone.Dispose();
        _cloud.Dispose();
        GC.SuppressFinalize(this);
    }
}
