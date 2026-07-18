using FluentValidation;
using JobBoard.Applications.Core.Facade;
using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Applications.Core.Managers.Validators;
using JobBoard.Applications.Tests.Fakes;
using Xunit;

namespace JobBoard.Applications.Tests;

public sealed class ApplicationFacadeTests
{
    private static readonly ApplicationDetailServiceModel AnyDetail = new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ApplicationStatus.Submitted, "resume-ref",
        DateTime.UtcNow, DateTime.UtcNow);

    private static ApplicationFacade CreateFacade(FakeApplicationBusiness business) =>
        new(business, new SubmitApplicationViewModelValidator(), new AdvanceApplicationStatusViewModelValidator());

    [Fact]
    public async Task SubmitAsync_ValidViewModel_DelegatesToBusiness()
    {
        var business = new FakeApplicationBusiness { Result = AnyDetail };
        var facade = CreateFacade(business);

        var result = await facade.SubmitAsync(TestData.SubmitViewModel());

        Assert.Equal(1, business.SubmitCallCount);
        Assert.Equal(AnyDetail.Id, result.Id);
    }

    [Fact]
    public async Task SubmitAsync_InvalidViewModel_Throws_AndNeverReachesBusiness()
    {
        var business = new FakeApplicationBusiness { Result = AnyDetail };
        var facade = CreateFacade(business);

        // Empty candidate id violates the validator — the global handler maps this to a 400.
        var invalid = TestData.SubmitViewModel(candidateId: Guid.Empty);

        await Assert.ThrowsAsync<ValidationException>(() => facade.SubmitAsync(invalid));
        Assert.Equal(0, business.SubmitCallCount);
    }

    [Fact]
    public async Task AdvanceAsync_ValidViewModel_DelegatesToBusiness()
    {
        var business = new FakeApplicationBusiness { Result = AnyDetail };
        var facade = CreateFacade(business);

        var result = await facade.AdvanceAsync(Guid.NewGuid(), TestData.AdvanceViewModel(ApplicationStatus.Reviewed));

        Assert.Equal(1, business.AdvanceCallCount);
        Assert.Equal(AnyDetail.Id, result.Id);
    }

    [Fact]
    public async Task AdvanceAsync_UndefinedTargetStatus_Throws_AndNeverReachesBusiness()
    {
        var business = new FakeApplicationBusiness { Result = AnyDetail };
        var facade = CreateFacade(business);

        // A value outside the enum fails IsInEnum — validation short-circuits before business.
        var invalid = TestData.AdvanceViewModel((ApplicationStatus)99);

        await Assert.ThrowsAsync<ValidationException>(() => facade.AdvanceAsync(Guid.NewGuid(), invalid));
        Assert.Equal(0, business.AdvanceCallCount);
    }
}
