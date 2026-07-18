using FluentValidation;
using JobBoard.Jobs.Core.Facade;
using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Jobs.Core.Managers.Validators;
using JobBoard.Jobs.Tests.Fakes;
using Xunit;

namespace JobBoard.Jobs.Tests;

public sealed class JobFacadeTests
{
    private static readonly JobDetailServiceModel AnyDetail = new(
        Guid.NewGuid(), "Senior Engineer", "Build things.", "Remote",
        new SalaryBandServiceModel(100_000m, 150_000m, "USD"),
        JobStatus.Open, Guid.NewGuid(), [], [], DateTime.UtcNow);

    private static JobFacade CreateFacade(FakeJobBusiness business) =>
        new(business, new PostJobViewModelValidator());

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
}
