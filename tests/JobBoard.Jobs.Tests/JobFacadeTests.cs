using FluentValidation;
using JobBoard.Jobs.Core.Facade;
using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Jobs.Core.Managers.Validators;
using JobBoard.Jobs.Tests.Fakes;
using JobBoard.Shared.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobBoard.Jobs.Tests;

public sealed class JobFacadeTests
{
    private static readonly JobDetailServiceModel AnyDetail = new(
        Guid.NewGuid(), "Senior Engineer", "Build things.", "Remote",
        new SalaryBandServiceModel(100_000m, 150_000m, "USD"),
        JobStatus.Open, Guid.NewGuid(), [], [], DateTime.UtcNow);

    private static readonly IReadOnlyList<JobSummaryServiceModel> AnyList =
    [
        new(Guid.NewGuid(), "Senior Engineer", "Remote",
            new SalaryBandServiceModel(100_000m, 150_000m, "USD"),
            JobStatus.Open, ["engineering"], DateTime.UtcNow),
    ];

    private static JobFacade CreateFacade(FakeJobBusiness business, FakeCache? cache = null) =>
        new(business, new PostJobViewModelValidator(), cache ?? new FakeCache(), NullLogger<JobFacade>.Instance);

    [Fact]
    public async Task PostAsync_ValidViewModel_DelegatesToBusiness()
    {
        var business = new FakeJobBusiness { Result = AnyDetail };
        var facade = CreateFacade(business);

        var result = await facade.PostAsync(TestData.PostViewModel());

        Assert.Equal(1, business.PostCallCount);
        Assert.Equal(AnyDetail.Id, result.Id);
    }

    [Fact]
    public async Task PostAsync_InvalidViewModel_Throws_AndNeverReachesBusiness()
    {
        var business = new FakeJobBusiness { Result = AnyDetail };
        var facade = CreateFacade(business);

        // Empty title violates the validator — the global handler maps this to a 400.
        var invalid = TestData.PostViewModel(title: "");

        await Assert.ThrowsAsync<ValidationException>(() => facade.PostAsync(invalid));
        Assert.Equal(0, business.PostCallCount);
    }

    [Fact]
    public async Task ListAsync_Miss_CallsBusiness_AndCachesResult()
    {
        var business = new FakeJobBusiness { ListResult = AnyList };
        var cache = new FakeCache();
        var facade = CreateFacade(business, cache);

        var result = await facade.ListAsync(categorySlug: null);

        Assert.Equal(1, business.ListCallCount);
        Assert.Same(AnyList, result);
        // Generation token + the list variant were both written through.
        Assert.True(cache.Contains("jobs:list:gen"));
        Assert.Equal(2, cache.SetCount);
    }

    [Fact]
    public async Task ListAsync_Hit_ReturnsCached_WithoutCallingBusiness()
    {
        var business = new FakeJobBusiness { ListResult = AnyList };
        var cache = new FakeCache();
        var facade = CreateFacade(business, cache);

        // Prime the cache, then read again against the same cache.
        await facade.ListAsync(categorySlug: null);
        var second = await facade.ListAsync(categorySlug: null);

        Assert.Equal(1, business.ListCallCount); // second read served from cache
        Assert.Same(AnyList, second);
    }

    [Fact]
    public async Task ListAsync_DifferentCategory_IsCachedSeparately()
    {
        var business = new FakeJobBusiness { ListResult = AnyList };
        var cache = new FakeCache();
        var facade = CreateFacade(business, cache);

        await facade.ListAsync(categorySlug: null);
        await facade.ListAsync(categorySlug: "engineering");

        // Distinct variants → business hit once each (no cross-variant bleed).
        Assert.Equal(2, business.ListCallCount);
    }

    [Fact]
    public async Task PostAsync_InvalidatesList_SoNextReadMissesAgain()
    {
        var business = new FakeJobBusiness { Result = AnyDetail, ListResult = AnyList };
        var cache = new FakeCache();
        var facade = CreateFacade(business, cache);

        await facade.ListAsync(categorySlug: null);     // miss → cached (business hit #1)
        await facade.PostAsync(TestData.PostViewModel()); // invalidates the generation token
        await facade.ListAsync(categorySlug: null);     // miss again → business hit #2

        Assert.Equal(2, business.ListCallCount);
        Assert.True(cache.RemoveCount >= 1);
    }

    [Fact]
    public async Task ListAsync_CacheThrows_FailsOpen_ServesFromBusiness()
    {
        var business = new FakeJobBusiness { ListResult = AnyList };
        var facade = new JobFacade(business, new PostJobViewModelValidator(), new ThrowingCache(), NullLogger<JobFacade>.Instance);

        var result = await facade.ListAsync(categorySlug: null);

        // A Redis outage degrades to the source rather than failing the read.
        Assert.Same(AnyList, result);
        Assert.Equal(1, business.ListCallCount);
    }

    [Fact]
    public async Task PostAsync_CacheInvalidationThrows_DoesNotFailTheWrite()
    {
        var business = new FakeJobBusiness { Result = AnyDetail, ListResult = AnyList };
        var facade = new JobFacade(business, new PostJobViewModelValidator(), new ThrowingCache(), NullLogger<JobFacade>.Instance);

        // The domain write has already committed by the time invalidation runs; a cache failure there
        // must not bubble up as a failed post.
        var result = await facade.PostAsync(TestData.PostViewModel());

        Assert.Equal(AnyDetail.Id, result.Id);
        Assert.Equal(1, business.PostCallCount);
    }

    // An ICache whose every operation throws — stands in for a Redis outage to prove fail-open behavior.
    private sealed class ThrowingCache : ICache
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("cache down");

        public Task SetAsync<T>(string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("cache down");

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("cache down");
    }

    [Fact]
    public async Task CloseAsync_InvalidatesList_SoNextReadMissesAgain()
    {
        var business = new FakeJobBusiness { Result = AnyDetail, ListResult = AnyList };
        var cache = new FakeCache();
        var facade = CreateFacade(business, cache);

        await facade.ListAsync(categorySlug: null);   // miss → cached (business hit #1)
        await facade.CloseAsync(AnyDetail.Id);         // invalidates the generation token
        await facade.ListAsync(categorySlug: null);   // miss again → business hit #2

        Assert.Equal(1, business.CloseCallCount);
        Assert.Equal(2, business.ListCallCount);
        Assert.True(cache.RemoveCount >= 1);
    }
}
