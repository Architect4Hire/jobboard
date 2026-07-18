using JobBoard.Identity.Core.Data;
using JobBoard.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Identity.Tests;

/// <summary>
/// Data-layer behaviour over a real (SQLite) context — the register insert commits through the
/// transaction, and the unique-email index turns a second registration into a mapped conflict rather than
/// a raw exception. A fresh context per operation mirrors the request-scoped lifetime in production.
/// </summary>
public sealed class AccountDataLayerTests
{
    private static AccountDataLayer CreateDataLayer(IdentityDbContext context) =>
        new(new AccountRepository(context));

    [Fact]
    public async Task RegisterAsync_PersistsAccount()
    {
        using var harness = new IdentitySqliteHarness();
        var account = TestData.Account(email: "persist@example.com");

        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).RegisterAsync(account);
        }

        await using var assert = harness.CreateContext();
        Assert.Equal("persist@example.com", (await assert.Accounts.SingleAsync()).Email);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsConflict()
    {
        using var harness = new IdentitySqliteHarness();

        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).RegisterAsync(TestData.Account(email: "taken@example.com"));
        }

        await using var second = harness.CreateContext();
        var dataLayer = CreateDataLayer(second);

        // A different account id, same email — the unique index trips inside the transaction.
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => dataLayer.RegisterAsync(TestData.Account(email: "taken@example.com")));

        Assert.Equal("account.email_taken", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task GetByEmailAsync_PassesThroughToRepository()
    {
        using var harness = new IdentitySqliteHarness();

        await using (var context = harness.CreateContext())
        {
            await CreateDataLayer(context).RegisterAsync(TestData.Account(email: "reader@example.com"));
        }

        await using var read = harness.CreateContext();
        var found = await CreateDataLayer(read).GetByEmailAsync("reader@example.com");

        Assert.NotNull(found);
        Assert.Equal("reader@example.com", found!.Email);
    }
}
