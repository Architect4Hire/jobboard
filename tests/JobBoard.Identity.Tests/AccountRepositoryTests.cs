using JobBoard.Identity.Core.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Identity.Tests;

public sealed class AccountRepositoryTests
{
    [Fact]
    public async Task IsDuplicateEmailViolation_TrueForRealEmailUniqueViolation()
    {
        using var harness = new IdentitySqliteHarness();

        await using var context = harness.CreateContext();
        context.Accounts.Add(TestData.Account(email: "dupe@example.com"));
        context.Accounts.Add(TestData.Account(email: "dupe@example.com"));

        // Two accounts with the same email violate the unique index on Email.
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.True(AccountRepository.IsDuplicateEmailViolation(ex));
    }

    [Fact]
    public void IsDuplicateEmailViolation_FalseForUnrelatedFailure()
    {
        var unrelated = new DbUpdateException("boom", new Exception("connection reset"));

        Assert.False(AccountRepository.IsDuplicateEmailViolation(unrelated));
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsMatchingAccount_OrNull()
    {
        using var harness = new IdentitySqliteHarness();
        var account = TestData.Account(email: "found@example.com");

        await using (var seed = harness.CreateContext())
        {
            seed.Accounts.Add(account);
            await seed.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var repository = new AccountRepository(context);

        var found = await repository.GetByEmailAsync("found@example.com");
        var missing = await repository.GetByEmailAsync("nobody@example.com");

        Assert.NotNull(found);
        Assert.Equal(account.Id, found!.Id);
        Assert.Null(missing);
    }

    [Fact]
    public async Task AddAsync_StagesAccount_ThatPersistsOnSave()
    {
        using var harness = new IdentitySqliteHarness();
        var account = TestData.Account(email: "new@example.com");

        await using (var context = harness.CreateContext())
        {
            var repository = new AccountRepository(context);
            await repository.AddAsync(account);
            await context.SaveChangesAsync();
        }

        await using var assert = harness.CreateContext();
        Assert.Equal("new@example.com", (await assert.Accounts.SingleAsync()).Email);
    }
}
