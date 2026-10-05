namespace FinanceApp.UnitTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using FinanceApp.Application.DTOs;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using Xunit;

/// <summary>
/// One user's data, created on a phone and then synced by a second device - the
/// situation that produced duplicated accounts and two accounts both badged
/// "Default".
/// <para>
/// Accounts are identified by a client-minted Guid on both sides, so a row pulled
/// from the cloud is only recognisable as the same account if it keeps that Guid.
/// When it did not, the next sync could not match the server row, built another
/// copy, and pushed that copy back up: one more duplicate per sync, forever.
/// </para>
/// <para>
/// These run the real transport against <see cref="PostgrestStub"/> rather than a
/// mocked interface, because the id handling lives inside the pull merge and a
/// mock stops exactly where the bug is.
/// </para>
/// </summary>
public class SecondDeviceSyncTests : IDisposable
{
    private readonly PostgrestStub _cloud = new();
    private readonly TestDevice _phone;
    private readonly TestDevice _secondDevice;

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public SecondDeviceSyncTests()
    {
        _phone = new TestDevice(_cloud, UserId);
        _secondDevice = new TestDevice(_cloud, UserId);
    }

    [Fact]
    public async Task ASecondDevice_ReceivesTheAccountsThePhoneCreated()
    {
        var phoneAccountId = await SeedPhoneAsync();
        await _phone.Sync().SyncAsync(UserId);

        // The second device signs in with an empty database. With nothing queued
        // for push, sync used to return before it ever pulled, so the device
        // stayed empty until the user re-created the accounts by hand - and those
        // copies were then pushed up beside the originals.
        await _secondDevice.Sync().SyncAsync(UserId);

        var cash = Assert.Single(_secondDevice.ReadAccounts());
        Assert.Equal("Cash", cash.Name);

        // The balance the phone arrived at, not the opening amount: the expense
        // came across too, so the figure is derived rather than copied.
        Assert.Equal(100m, cash.Balance);
        Assert.Equal(1, _secondDevice.CountTransactions());

        // One row on the server, under the id the phone gave it.
        Assert.Equal(1, _cloud.Count("accounts"));
        Assert.Contains(phoneAccountId.ToString(), _cloud.Rows("accounts").Keys);
    }

    [Fact]
    public async Task RepeatedSyncs_DoNotMultiplyTheAccounts()
    {
        await SeedPhoneAsync();
        await _phone.Sync().SyncAsync(UserId);

        await _secondDevice.Sync().SyncAsync(UserId);
        await _secondDevice.Sync().SyncAsync(UserId);
        await _secondDevice.Sync().SyncAsync(UserId);

        // The pull used to build each server row under a brand new id, so the
        // server row never matched what was already here. Every sync added another
        // copy locally and pushed it back up, and this count grew by one each time.
        Assert.Single(_secondDevice.ReadAccounts());
        Assert.Equal(1, _cloud.Count("accounts"));
    }

    [Fact]
    public async Task ATwoWayExchange_SettlesInsteadOfGrowingForever()
    {
        await SeedPhoneAsync();
        await _phone.Sync().SyncAsync(UserId);
        await _secondDevice.Sync().SyncAsync(UserId);

        // The second device records its own expense.
        var account = (await _secondDevice.Accounts.GetByUserIdAsync(UserId)).Single();
        var food = new Category("Food", CategoryType.Expense, UserId);
        await _secondDevice.Categories.AddAsync(food);
        await _secondDevice.Work.SaveChangesAsync();

        await _secondDevice.Transactions.AddAsync(new Transaction(
            TransactionType.Expense, new Money(25), DateTime.Today,
            new AccountId(account.Id), (CategoryId)food.Id, UserId));
        await _secondDevice.Work.SaveChangesAsync();

        // ...and must not bring a duplicate of the account back with it.
        await _secondDevice.Sync().SyncAsync(UserId);

        Assert.Single(_secondDevice.ReadAccounts());
        Assert.Equal(1, _cloud.Count("accounts"));

        // 200 opening, less the phone's 100 and this device's 25.
        Assert.Equal(75m, Assert.Single(_secondDevice.ReadAccounts()).Balance);
    }

    [Fact]
    public async Task OnlyOneAccountIsTheDefault_AfterALocalAccountMeetsASyncedOne()
    {
        await SeedPhoneAsync();
        await _phone.Sync().SyncAsync(UserId);

        // The device made an account of its own before the phone's arrived. With
        // no default in existence it became the default, exactly as it does when a
        // user adds their first account.
        await _secondDevice.Accounts_().CreateAsync(
            new CreateAccountDto("Gcash", AccountType.EWallet, Money.Zero()), UserId);

        Assert.Equal(1, _secondDevice.CountDefaults());

        // Then the phone's default arrives. Two accounts badged "Default" is what
        // the user was looking at, and which one the app treats as the default
        // stops being well defined.
        await _secondDevice.Sync().SyncAsync(UserId);

        Assert.Equal(2, _secondDevice.ReadAccounts().Count);
        Assert.Equal(1, _secondDevice.CountDefaults());
    }

    /// <summary>The phone: Cash opened at 200, then a 100 expense against it.</summary>
    private async Task<Guid> SeedPhoneAsync()
    {
        var cash = new Account("Cash", AccountType.Cash, new Money(200), UserId, isDefault: true);
        await _phone.Accounts.AddAsync(cash);

        var food = new Category("Food", CategoryType.Expense, UserId);
        await _phone.Categories.AddAsync(food);
        await _phone.Work.SaveChangesAsync();

        await _phone.Transactions.AddAsync(new Transaction(
            TransactionType.Expense, new Money(100), DateTime.Today,
            new AccountId(cash.Id), (CategoryId)food.Id, UserId));
        await _phone.Work.SaveChangesAsync();

        await _phone.Balances.RecalculateAsync(cash.Id);
        return cash.Id;
    }

    public void Dispose()
    {
        _secondDevice.Dispose();
        _phone.Dispose();
        _cloud.Dispose();
        GC.SuppressFinalize(this);
    }

}
