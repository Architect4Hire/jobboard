using JobBoard.Applications.Core.Data;
using JobBoard.Applications.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Applications.Tests;

public sealed class ApplicationRepositoryTests
{
    [Fact]
    public async Task IsDuplicateApplicationViolation_TrueForRealUniqueViolation()
    {
        using var harness = new ApplicationsSqliteHarness();
        var candidateId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        await using var context = harness.CreateContext();
        context.Applications.Add(TestData.Application(candidateId: candidateId, jobId: jobId));
        context.Applications.Add(TestData.Application(candidateId: candidateId, jobId: jobId));

        // Two applications for the same candidate + job violate the unique index.
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.True(ApplicationRepository.IsDuplicateApplicationViolation(ex));
    }

    [Fact]
    public void IsDuplicateApplicationViolation_FalseForUnrelatedFailure()
    {
        var unrelated = new DbUpdateException("boom", new Exception("connection reset"));

        Assert.False(ApplicationRepository.IsDuplicateApplicationViolation(unrelated));
    }

    [Fact]
    public async Task ListByCandidateAsync_ReturnsOnlyThatCandidate_ProjectedToSummaries_NewestFirst()
    {
        using var harness = new ApplicationsSqliteHarness();
        var candidateId = Guid.NewGuid();

        var older = TestData.Application(candidateId: candidateId, status: ApplicationStatus.Reviewed);
        older.SubmittedOnUtc = DateTime.UtcNow.AddHours(-1);
        var newer = TestData.Application(candidateId: candidateId, status: ApplicationStatus.Submitted);
        newer.SubmittedOnUtc = DateTime.UtcNow;

        await using (var seed = harness.CreateContext())
        {
            seed.Applications.Add(older);
            seed.Applications.Add(newer);
            seed.Applications.Add(TestData.Application()); // a different candidate
            await seed.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var repository = new ApplicationRepository(context);

        var results = await repository.ListByCandidateAsync(candidateId);

        Assert.Equal(2, results.Count);
        Assert.Equal(newer.Id, results[0].Id);   // newest first
        Assert.Equal(older.Id, results[1].Id);
    }

    [Fact]
    public async Task GetActiveByJobAsync_ReturnsOnlyActiveApplicationsForThatJob()
    {
        using var harness = new ApplicationsSqliteHarness();
        var jobId = Guid.NewGuid();

        var submitted = TestData.Application(jobId: jobId, status: ApplicationStatus.Submitted);
        var offered = TestData.Application(jobId: jobId, status: ApplicationStatus.Offered);
        var rejected = TestData.Application(jobId: jobId, status: ApplicationStatus.Rejected);
        var withdrawn = TestData.Application(jobId: jobId, status: ApplicationStatus.Withdrawn);
        var otherJob = TestData.Application(status: ApplicationStatus.Submitted);

        await using (var seed = harness.CreateContext())
        {
            seed.Applications.AddRange(submitted, offered, rejected, withdrawn, otherJob);
            await seed.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var repository = new ApplicationRepository(context);

        var active = await repository.GetActiveByJobAsync(jobId);

        Assert.Equal(
            new[] { submitted.Id, offered.Id }.OrderBy(id => id),
            active.Select(a => a.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task WithdrawIfActiveAsync_TrueWhenActive_FalseWhenTerminal()
    {
        using var harness = new ApplicationsSqliteHarness();
        var active = TestData.Application(status: ApplicationStatus.Reviewed);
        var terminal = TestData.Application(status: ApplicationStatus.Rejected);

        await using (var seed = harness.CreateContext())
        {
            seed.Applications.AddRange(active, terminal);
            await seed.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var repository = new ApplicationRepository(context);

        Assert.True(await repository.WithdrawIfActiveAsync(active.Id));
        Assert.False(await repository.WithdrawIfActiveAsync(terminal.Id));

        await using var assert = harness.CreateContext();
        Assert.Equal(ApplicationStatus.Withdrawn, (await assert.Applications.FindAsync(active.Id))!.Status);
        Assert.Equal(ApplicationStatus.Rejected, (await assert.Applications.FindAsync(terminal.Id))!.Status);
    }

    [Fact]
    public async Task AdvanceIfInStatusAsync_TrueOnlyWhenCurrentMatchesExpected()
    {
        using var harness = new ApplicationsSqliteHarness();
        var application = TestData.Application(status: ApplicationStatus.Submitted);

        await using (var seed = harness.CreateContext())
        {
            seed.Applications.Add(application);
            await seed.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var repository = new ApplicationRepository(context);

        // Wrong expected → no change.
        Assert.False(await repository.AdvanceIfInStatusAsync(application.Id, ApplicationStatus.Reviewed, ApplicationStatus.Offered));
        // Right expected → advances.
        Assert.True(await repository.AdvanceIfInStatusAsync(application.Id, ApplicationStatus.Submitted, ApplicationStatus.Reviewed));

        await using var assert = harness.CreateContext();
        Assert.Equal(ApplicationStatus.Reviewed, (await assert.Applications.FindAsync(application.Id))!.Status);
    }

    [Fact]
    public async Task CloseActiveByJobAsync_ClosesActive_LeavesTerminalAndOtherJobs()
    {
        using var harness = new ApplicationsSqliteHarness();
        var jobId = Guid.NewGuid();

        var submitted = TestData.Application(jobId: jobId, status: ApplicationStatus.Submitted);
        var offered = TestData.Application(jobId: jobId, status: ApplicationStatus.Offered);
        var withdrawn = TestData.Application(jobId: jobId, status: ApplicationStatus.Withdrawn);
        var otherJob = TestData.Application(status: ApplicationStatus.Submitted);

        await using (var seed = harness.CreateContext())
        {
            seed.Applications.AddRange(submitted, offered, withdrawn, otherJob);
            await seed.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var repository = new ApplicationRepository(context);

        var closed = await repository.CloseActiveByJobAsync(jobId, ApplicationStatus.Rejected);

        Assert.Equal(2, closed);

        await using var assert = harness.CreateContext();
        Assert.Equal(ApplicationStatus.Rejected, (await assert.Applications.FindAsync(submitted.Id))!.Status);
        Assert.Equal(ApplicationStatus.Rejected, (await assert.Applications.FindAsync(offered.Id))!.Status);
        Assert.Equal(ApplicationStatus.Withdrawn, (await assert.Applications.FindAsync(withdrawn.Id))!.Status);
        Assert.Equal(ApplicationStatus.Submitted, (await assert.Applications.FindAsync(otherJob.Id))!.Status);
    }
}
