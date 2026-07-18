using JobBoard.Jobs.Core.Data;
using JobBoard.Jobs.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Jobs.Tests;

public sealed class JobRepositoryTests
{
    [Fact]
    public async Task IsDuplicateSlugViolation_TrueForRealSlugUniqueViolation()
    {
        using var harness = new JobsSqliteHarness();

        await using var context = harness.CreateContext();
        context.Categories.Add(TestData.Category("engineering"));
        context.Categories.Add(TestData.Category("engineering", "Engineering (dupe)"));

        // Two categories with the same slug violate the unique index on Slug.
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.True(JobRepository.IsDuplicateSlugViolation(ex));
    }

    [Fact]
    public void IsDuplicateSlugViolation_FalseForUnrelatedFailure()
    {
        var unrelated = new DbUpdateException("boom", new Exception("connection reset"));

        Assert.False(JobRepository.IsDuplicateSlugViolation(unrelated));
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyOpenJobs_ProjectedToSummaries()
    {
        using var harness = new JobsSqliteHarness();
        var engineering = TestData.Category("engineering");

        await using (var seed = harness.CreateContext())
        {
            seed.Jobs.Add(TestData.Job(status: JobStatus.Open, title: "Open one", categories: [engineering]));
            seed.Jobs.Add(TestData.Job(status: JobStatus.Closed, title: "Closed one", categories: [TestData.Category("engineering-2", "Eng2")]));
            seed.Jobs.Add(TestData.Job(status: JobStatus.Draft, title: "Draft one"));
            await seed.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var repository = new JobRepository(context);

        var results = await repository.ListAsync(categorySlug: null);

        var summary = Assert.Single(results);
        Assert.Equal("Open one", summary.Title);
        Assert.Equal(JobStatus.Open, summary.Status);
        Assert.Equal("USD", summary.Salary.Currency);
        Assert.Equal(["engineering"], summary.CategorySlugs);
    }

    [Fact]
    public async Task ListAsync_FiltersByCategorySlug()
    {
        using var harness = new JobsSqliteHarness();

        await using (var seed = harness.CreateContext())
        {
            seed.Jobs.Add(TestData.Job(title: "Eng job", categories: [TestData.Category("engineering")]));
            seed.Jobs.Add(TestData.Job(title: "Design job", categories: [TestData.Category("design", "Design")]));
            await seed.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var repository = new JobRepository(context);

        var results = await repository.ListAsync(categorySlug: "design");

        Assert.Equal("Design job", Assert.Single(results).Title);
    }

    [Fact]
    public async Task GetAsync_IncludesCategoriesAndTags()
    {
        using var harness = new JobsSqliteHarness();
        var job = TestData.Job(categories: [TestData.Category("engineering")], tags: [TestData.Tag("remote")]);

        await using (var seed = harness.CreateContext())
        {
            seed.Jobs.Add(job);
            await seed.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var repository = new JobRepository(context);

        var loaded = await repository.GetAsync(job.Id);

        Assert.NotNull(loaded);
        Assert.Equal("engineering", Assert.Single(loaded!.Categories).Slug);
        Assert.Equal("remote", Assert.Single(loaded.Tags).Slug);
    }

    [Fact]
    public async Task AddAsync_ReusesExistingClassification_BySlug()
    {
        using var harness = new JobsSqliteHarness();

        // An "engineering" category already exists (e.g. from an earlier post).
        Guid existingCategoryId;
        await using (var seed = harness.CreateContext())
        {
            var existing = TestData.Category("engineering");
            existingCategoryId = existing.Id;
            seed.Categories.Add(existing);
            await seed.SaveChangesAsync();
        }

        // A new post references the same slug (different id) plus a brand-new tag.
        await using (var context = harness.CreateContext())
        {
            var repository = new JobRepository(context);
            var job = TestData.Job(
                categories: [TestData.Category("engineering")],
                tags: [TestData.Tag("remote")]);

            await repository.AddAsync(job);
            await context.SaveChangesAsync();
        }

        await using var assert = harness.CreateContext();
        // The category was reused, not duplicated; the tag was created.
        Assert.Equal(existingCategoryId, Assert.Single(await assert.Categories.ToListAsync()).Id);
        Assert.Equal("remote", Assert.Single(await assert.Tags.ToListAsync()).Slug);
        Assert.Single(await assert.Jobs.ToListAsync());
    }
}
