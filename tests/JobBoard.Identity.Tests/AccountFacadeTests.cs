using FluentValidation;
using JobBoard.Identity.Core.Facade;
using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Identity.Core.Managers.Models.ServiceModels;
using JobBoard.Identity.Core.Managers.Validators;
using JobBoard.Identity.Tests.Fakes;
using Xunit;

namespace JobBoard.Identity.Tests;

public sealed class AccountFacadeTests
{
    private static readonly AccountServiceModel AnyAccount =
        new(Guid.NewGuid(), "user@example.com", AccountRole.Candidate);

    private static readonly AuthTokenServiceModel AnyToken =
        new("token", "Bearer", DateTime.UtcNow.AddHours(1));

    private static AccountFacade CreateFacade(FakeAccountBusiness business) =>
        new(business, new RegisterAccountViewModelValidator(), new LoginViewModelValidator());

    [Fact]
    public async Task RegisterAsync_ValidViewModel_DelegatesToBusiness()
    {
        var business = new FakeAccountBusiness { RegisterResult = AnyAccount };
        var facade = CreateFacade(business);

        var result = await facade.RegisterAsync(TestData.RegisterViewModel());

        Assert.Equal(1, business.RegisterCallCount);
        Assert.Equal(AnyAccount.Id, result.Id);
    }

    [Fact]
    public async Task RegisterAsync_InvalidEmail_Throws_AndNeverReachesBusiness()
    {
        var business = new FakeAccountBusiness { RegisterResult = AnyAccount };
        var facade = CreateFacade(business);

        var invalid = TestData.RegisterViewModel(email: "not-an-email");

        await Assert.ThrowsAsync<ValidationException>(() => facade.RegisterAsync(invalid));
        Assert.Equal(0, business.RegisterCallCount);
    }

    [Fact]
    public async Task RegisterAsync_ShortPassword_Throws_AndNeverReachesBusiness()
    {
        var business = new FakeAccountBusiness { RegisterResult = AnyAccount };
        var facade = CreateFacade(business);

        var invalid = TestData.RegisterViewModel(password: "short");

        await Assert.ThrowsAsync<ValidationException>(() => facade.RegisterAsync(invalid));
        Assert.Equal(0, business.RegisterCallCount);
    }

    [Fact]
    public async Task LoginAsync_ValidViewModel_DelegatesToBusiness()
    {
        var business = new FakeAccountBusiness { LoginResult = AnyToken };
        var facade = CreateFacade(business);

        var result = await facade.LoginAsync(TestData.LoginViewModel());

        Assert.Equal(1, business.AuthenticateCallCount);
        Assert.Equal("token", result.AccessToken);
    }

    [Fact]
    public async Task LoginAsync_MissingPassword_Throws_AndNeverReachesBusiness()
    {
        var business = new FakeAccountBusiness { LoginResult = AnyToken };
        var facade = CreateFacade(business);

        var invalid = TestData.LoginViewModel(password: "");

        await Assert.ThrowsAsync<ValidationException>(() => facade.LoginAsync(invalid));
        Assert.Equal(0, business.AuthenticateCallCount);
    }
}
