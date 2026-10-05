namespace FinanceApp.UnitTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using Xunit;

/// <summary>
/// The rows an older build left duplicated in Supabase - two accounts, two
/// categories or two copies of one expense for a single real record - and what a
/// sync does about them now.
/// <para>
/// That build minted a fresh id for every server row it pulled, so the row it
/// created could never be matched again: the next push put it up beside the
/// original and every sync after that added another. The copies are still out
/// there, both in Supabase and in the app databases that hold them, and the pull
/// is the only moment a device sees both of them - so that is where they are
/// merged, the references are moved onto the one kept, and the deletion of the
/// other is pushed after them.
/// </para>
/// <para>
/// As in <see cref="SecondDeviceSyncTests"/>, these run the real transport
/// against <see cref="PostgrestStub"/>: what has to work is the interaction
/// between the pull, the merge and two passes of the push, and a mocked
/// interface stops short of all three.
/// </para>
/// </summary>
public class SyncDuplicateCleanupTests : IDisposable
{
    private readonly PostgrestStub _cloud = new();
    private readonly TestDevice _phone;
    private readonly TestDevice _secondDevice;

    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public SyncDuplicateCleanupTests()
    {
        _phone = new TestDevice(_cloud);
        _secondDevice = new TestDevice(_cloud);
    }

    [Fact]
    public async Task AnAccountTheOldPullDuplicated_IsMergedAndTakenOutOfSupabase()
    {
        await SeedPhoneAsync();
        await _phone.Sync().SyncAsync(UserId);
        await _secondDevice.Sync().SyncAsync(UserId);

        // What that build left in Supabase: the account again under an id of its
        // own, and the expense the user recorded against the copy.
        var copyId = _cloud.Duplicate("accounts", OnlyRow("accounts"), ("id", NewId()));
        _cloud.Duplicate("transactions", OnlyRow("transactions"), ("id", NewId()), ("account_id", copyId), ("amount", 25m), ("notes", "Garden supplies"));

        await _secondDevice.Sync().SyncAsync(UserId);

        // One account left, and Supabase holds that one under the same id.
        var survivor = Assert.Single(_secondDevice.LiveIds("Accounts"));
        Assert.Equal(survivor, Assert.Single(_cloud.Rows("accounts").Keys), StringComparer.OrdinalIgnoreCase);

        // Both expenses still recorded, against the account that survived: the
        // spare's rows were moved before it was dropped, not dropped with them.
        Assert.Equal(2, _secondDevice.CountTransactions());
        Assert.Equal(2, _cloud.Count("transactions"));
        Assert.All(_secondDevice.TransactionLinks(), link => Assert.Equal(survivor, link.AccountId, StringComparer.OrdinalIgnoreCase));

        // The spare is a tombstone rather than a hole. Destroying it would let
        // the next pull read the server copy straight back into the app.
        Assert.Equal(2, _secondDevice.CountAnyState("Accounts"));

        // 200 opening on both copies, less the phone's 100 and the copy's 25.
        Assert.Equal(75m, Assert.Single(_secondDevice.ReadAccounts()).Balance);

        // The update that re-pointed the expense landed before the delete that
        // removed the account it used to point at: the server refuses to drop a
        // row something still references.
        var requests = _cloud.Requests.ToList();
        var lastExpensePush = requests.FindLastIndex(r => r.StartsWith("POST /rest/v1/transactions", StringComparison.Ordinal));
        var accountDelete = requests.FindIndex(r => r.StartsWith("DELETE /rest/v1/accounts", StringComparison.Ordinal));
        Assert.True(
            accountDelete >= 0 && accountDelete > lastExpensePush,
            $"Expected the account delete after the last expense push. Requests:\n{string.Join("\n", requests)}");
    }

    [Fact]
    public async Task TheSameExpenseRecordedTwice_IsWorthOnlyOne()
    {
        await SeedPhoneAsync();
        await _phone.Sync().SyncAsync(UserId);
        await _secondDevice.Sync().SyncAsync(UserId);

        // The same expense again, everything about it alike but the id.
        _cloud.Duplicate("transactions", OnlyRow("transactions"), ("id", NewId()));

        await _secondDevice.Sync().SyncAsync(UserId);

        Assert.Equal(1, _secondDevice.CountTransactions());
        Assert.Equal(1, _cloud.Count("transactions"));

        // Counted once: a second copy would put this at 0 or -100.
        Assert.Equal(100m, Assert.Single(_secondDevice.ReadAccounts()).Balance);
    }

    [Fact]
    public async Task ADefaultCategoryTwoDevicesSeededApart_IsMergedByName()
    {
        await SeedPhoneAsync();
        await _phone.Sync().SyncAsync(UserId);
        await _secondDevice.Sync().SyncAsync(UserId);

        // Every device that opens the app makes its own copy of the built-in
        // categories on whatever day it first ran, so this one reached Supabase
        // three days before the phone's and under an id of its own. Only the
        // name says the two are one category.
        var phoneFood = _cloud.Rows("categories").Keys.Single();
        _cloud.Duplicate(
            "categories",
            phoneFood,
            ("id", NewId()),
            ("created_at", _foodCreated.AddDays(-3).ToString("o")));

        await _secondDevice.Sync().SyncAsync(UserId);

        var survivor = Assert.Single(_secondDevice.LiveIds("Categories"));
        Assert.Equal(survivor, Assert.Single(_cloud.Rows("categories").Keys), StringComparer.OrdinalIgnoreCase);

        // The expense was pointed at the category the copy carried; it is pointed
        // at the one kept now.
        Assert.All(_secondDevice.TransactionLinks(), link => Assert.Equal(survivor, link.CategoryId, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(100m, Assert.Single(_secondDevice.ReadAccounts()).Balance);
    }

    private DateTime _foodCreated;

    /// <summary>The phone: Cash opened at 200, then a 100 expense against it.</summary>
    private async Task SeedPhoneAsync()
    {
        var cash = new Account("Cash", AccountType.Cash, new Money(200), UserId, isDefault: true);
        await _phone.Accounts.AddAsync(cash);

        var food = new Category("Food", CategoryType.Expense, UserId, isSystem: true);
        await _phone.Categories.AddAsync(food);
        await _phone.Work.SaveChangesAsync();

        _foodCreated = food.CreatedAt;

        await _phone.Transactions.AddAsync(new Transaction(
            TransactionType.Expense, new Money(100), DateTime.Today,
            new AccountId(cash.Id), (CategoryId)food.Id, UserId));
        await _phone.Work.SaveChangesAsync();

        await _phone.Balances.RecalculateAsync(cash.Id);
    }

    private string OnlyRow(string table) => _cloud.Rows(table).Keys.Single();

    private static string NewId() => Guid.NewGuid().ToString();

    public void Dispose()
    {
        _secondDevice.Dispose();
        _phone.Dispose();
        _cloud.Dispose();
        GC.SuppressFinalize(this);
    }
}
