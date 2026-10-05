namespace FinanceApp.UnitTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using FinanceApp.Domain.ValueObjects;
using Xunit;

/// <summary>
/// "new row violates row-level security policy for table categories" - the error
/// every user saw on every push.
/// <para>
/// The policies in the schema are <c>to authenticated</c> and keyed on
/// <c>auth.uid() = user_id</c>. A client holding no session still sends the
/// request: the SDK presents the anon key as <c>Authorization: Bearer</c>, so
/// Postgres runs the statement as <c>anon</c> with <c>auth.uid()</c> null and
/// refuses it. Every push went out that way, so every one failed, and the retries
/// burned through until the queue reported the user's changes as failures.
/// </para>
/// <para>
/// These run against <see cref="PostgrestStub"/> with its policies switched on
/// rather than against a mock, because the thing being asserted is the identity
/// the request went out with - a mocked transport is attached after that and
/// cannot see it.
/// </para>
/// </summary>
public class SyncRowLevelSecurityTests : IDisposable
{
    private readonly PostgrestStub _cloud = new() { EnforceRowLevelSecurity = true };

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public void Dispose()
    {
        _cloud.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task WithoutASignedInSession_ThePushIsNeverSent()
    {
        using var device = new TestDevice(_cloud);
        await SeedAsync(device);

        var result = await device.Sync().SyncAsync(UserId);

        // Nothing reached the server, which is the point: a request sent as the
        // anon key cannot be admitted by any policy in the schema.
        Assert.Equal(0, _cloud.Count("categories"));
        Assert.DoesNotContain(_cloud.Requests, r => r.Contains("categories", StringComparison.Ordinal));

        // Not even an identity was offered. The client is holding the anon key and
        // no session, and the only header the schema could have read auth.uid()
        // from was the one that would have condemned it.
        Assert.Empty(_cloud.Headers);
    }

    [Fact]
    public async Task WhatTheAnonymousClientWouldHaveSent_IsTheAnonKeyAsABearer()
    {
        // The mechanism the two tests above depend on: with no session, the SDK
        // still sends the request, presenting the anon key as the bearer token.
        // Postgres reads that as the anon role with auth.uid() null, which is what
        // every policy in the schema excludes - hence the refusal, and hence the
        // refusal arriving as one identical error for all six tables.
        var client = new global::Supabase.Client(_cloud.Url, _cloud.AnonKey,
            new global::Supabase.SupabaseOptions { AutoConnectRealtime = false });
        await client.InitializeAsync();

        Assert.Null(client.Auth.CurrentSession);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.From<FinanceApp.Infrastructure.Supabase.Models.CategoryRecord>()
                .Insert(new FinanceApp.Infrastructure.Supabase.Models.CategoryRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = UserId,
                    Name = "Food",
                    Type = "Expense"
                }));

        Assert.Equal($"Bearer {_cloud.AnonKey}", _cloud.Headers["Authorization"]);
    }

    [Fact]
    public async Task WithoutASignedInSession_TheChangesStayPending_AndKeepTheirRetries()
    {
        using var device = new TestDevice(_cloud);
        await SeedAsync(device);

        var result = await device.Sync().SyncAsync(UserId);

        // Reported as paused rather than failed. "Failed" would be a lie in both
        // directions: nothing was rejected, and the queue would burn its retry
        // budget on a server that was never asked.
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.SyncedCount);
        Assert.True(result.DeferredCount > 0);
        Assert.False(result.Success);

        var pending = await device.SyncOps.GetPendingByUserIdAsync(UserId);
        Assert.NotEmpty(pending);
        Assert.All(pending!, op => Assert.Equal(0, op.RetryCount));

        // The retry budget is what would eventually have retired these as failures.
        // Leaving it untouched is what lets the next tick push them for real.
        var status = await device.Sync().GetStatusAsync(UserId);
        Assert.Equal(0, status.FailedCount);
        Assert.True(status.PendingCount > 0);
    }

    [Fact]
    public async Task TheMessageSaysWhatToDo_NotWhatPostgresSaid()
    {
        using var device = new TestDevice(_cloud);
        await SeedAsync(device);

        var result = await device.Sync().SyncAsync(UserId);

        // The user cannot act on a Postgres error code. This is the state the app
        // gets into and the one thing that clears it.
        Assert.Contains("signed in", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("42501", result.ErrorMessage);
        Assert.DoesNotContain("row-level security", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WithASignedInSession_TheSamePushSucceeds()
    {
        using var device = new TestDevice(_cloud, UserId);
        await SeedAsync(device);

        var result = await device.Sync().SyncAsync(UserId);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.DeferredCount);
        Assert.NotEmpty(_cloud.Rows("categories"));

        // The row carries the id the policies compare against.
        var row = Assert.Single(_cloud.Rows("categories").Values);
        Assert.Contains(UserId.ToString(), row);

        var status = await device.Sync().GetStatusAsync(UserId);
        Assert.Equal(0, status.PendingCount);
        Assert.Equal(0, status.FailedCount);
    }

    [Fact]
    public async Task ASignedInAsSomebodyElse_ItSaysSoRatherThanFailingTheRow()
    {
        var other = Guid.Parse("99999999-9999-9999-9999-999999999999");
        using var device = new TestDevice(_cloud, other);
        await SeedAsync(device);

        var result = await device.Sync().SyncAsync(UserId);

        // Same 42501 from the other end, and a different remedy, so it has to be
        // named differently: waiting does not help here, signing in again does.
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, _cloud.Count("categories"));
        Assert.Contains("different account", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>One category and the account it belongs to - enough to queue a push.</summary>
    private static async Task SeedAsync(TestDevice device)
    {
        var account = new Account("Cash", AccountType.Cash, new Money(200), UserId, isDefault: true);
        await device.Accounts.AddAsync(account);

        await device.Categories.AddAsync(new Category("Food", CategoryType.Expense, UserId));
        await device.Work.SaveChangesAsync();
    }
}