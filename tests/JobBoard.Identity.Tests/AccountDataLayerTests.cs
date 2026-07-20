using JobBoard.Contracts;
using JobBoard.Identity.Core.Data;
using JobBoard.Identity.Core.Managers.Mappers;
using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Identity.Tests.Fakes;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Identity.Tests;

/// <summary>
/// Data-layer behaviour over a real (SQLite) context — the register insert and its <see cref="AccountCreated"/>
/// outbox row commit through one transaction, and the unique-email index turns a second registration into a
/// mapped conflict rather than a raw exception. A fresh context per operation mirrors the request-scoped
/// lifetime in production.
/// </summary>
public sealed class AccountDataLayerTests
{
    private static AccountDataLayer CreateDataLayer(IdentityDbContext context, IOutbox? outbox = null) =>
        new(new AccountRepository(context), outbox ?? new Outbox(context));

    private static async Task RegisterAsync(AccountDataLayer dataLayer, Account account) =>
        await dataLayer.RegisterAsync(account, account.ToAccountCreated(default));

    [Fact]
    public async Task RegisterAsync_PersistsAccount_AndEnqueuesAccountCreated()
    {
        using var harness = new IdentitySqliteHarness();
        var account = TestData.Account(email: "persist@example.com");

        await using (var context = harness.CreateContext())
        {
            await RegisterAsync(CreateDataLayer(context), account);
        }

        await using var assert = harness.CreateContext();
        Assert.Equal("persist@example.com", (await assert.Accounts.SingleAsync()).Email);

        // The account and its outbox row committed together; the row ships to the AccountCreated topic.
        var row = await assert.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(AccountCreated), row.Type);
        Assert.Equal(nameof(AccountCreated), row.Destination);
    }

    [Fact]
    public async Task RegisterAsync_LeavesNoAccountAndNoOutboxRow_WhenEnqueueThrows()
    {
        using var harness = new IdentitySqliteHarness();
        var account = TestData.Account(email: "rollback@example.com");

        await using (var context = harness.CreateContext())
        {
            // The insert stages inside the transaction, then the AccountCreated outbox write throws — the
            // account must roll back with it, leaving nothing committed.
            var dataLayer = CreateDataLayer(context, new FakeOutbox { ThrowOnEnqueue = true });
            await Assert.ThrowsAsync<InvalidOperationException>(() => RegisterAsync(dataLayer, account));
        }

        await using var assert = harness.CreateContext();
        Assert.Empty(await assert.Accounts.ToListAsync());
        Assert.Empty(await assert.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsConflict_AndEnqueuesNothing()
    {
        using var harness = new IdentitySqliteHarness();

        await using (var context = harness.CreateContext())
        {
            await RegisterAsync(CreateDataLayer(context), TestData.Account(email: "taken@example.com"));
        }

        await using var second = harness.CreateContext();
        var dataLayer = CreateDataLayer(second);

        // A different account id, same email — the unique index trips inside the transaction.
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => RegisterAsync(dataLayer, TestData.Account(email: "taken@example.com")));

        Assert.Equal("account.email_taken", ex.Code);
        Assert.Equal(409, ex.StatusCode);

        // Exactly the first registration's row exists — the losing insert rolled back with its outbox write.
        await using var assert = harness.CreateContext();
        Assert.Single(await assert.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task RecordLoginAsync_EnqueuesLoggedIn_WithoutTouchingAccounts()
    {
        using var harness = new IdentitySqliteHarness();
        var account = TestData.Account(email: "signin@example.com");
        var loggedIn = account.ToLoggedIn(default);

        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).RecordLoginAsync(loggedIn);
        }

        await using var assert = harness.CreateContext();
        // Login mutates no domain state — only the outbox row exists, shipping to the LoggedIn topic.
        Assert.Empty(await assert.Accounts.ToListAsync());
        var row = await assert.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(LoggedIn), row.Type);
        Assert.Equal(nameof(LoggedIn), row.Destination);
    }

    [Fact]
    public async Task RecordLoginFailedAsync_EnqueuesLoginFailed()
    {
        using var harness = new IdentitySqliteHarness();
        var failed = AccountMappers.ToLoginFailed("nobody@example.com", default);

        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).RecordLoginFailedAsync(failed);
        }

        await using var assert = harness.CreateContext();
        var row = await assert.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(LoginFailed), row.Type);
        Assert.Equal(nameof(LoginFailed), row.Destination);
    }

    [Fact]
    public async Task RecordLoginAsync_LeavesNoOutboxRow_WhenEnqueueThrows()
    {
        using var harness = new IdentitySqliteHarness();
        var loggedIn = TestData.Account().ToLoggedIn(default);

        await using (var context = harness.CreateContext())
        {
            var dataLayer = CreateDataLayer(context, new FakeOutbox { ThrowOnEnqueue = true });
            await Assert.ThrowsAsync<InvalidOperationException>(() => dataLayer.RecordLoginAsync(loggedIn));
        }

        await using var assert = harness.CreateContext();
        Assert.Empty(await assert.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task GetByEmailAsync_PassesThroughToRepository()
    {
        using var harness = new IdentitySqliteHarness();

        await using (var context = harness.CreateContext())
        {
            await RegisterAsync(CreateDataLayer(context), TestData.Account(email: "reader@example.com"));
        }

        await using var read = harness.CreateContext();
        var found = await CreateDataLayer(read).GetByEmailAsync("reader@example.com");

        Assert.NotNull(found);
        Assert.Equal("reader@example.com", found!.Email);
    }
}
