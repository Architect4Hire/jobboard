using JobBoard.Identity.Core.Business;
using JobBoard.Identity.Core.Managers.Models.ServiceModels;
using JobBoard.Identity.Core.Managers.Models.ViewModels;

namespace JobBoard.Identity.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IAccountBusiness"/> for facade tests. Records whether each operation was
/// reached (so a test can prove validation short-circuits before business) and returns configured results.
/// </summary>
public sealed class FakeAccountBusiness : IAccountBusiness
{
    public AccountServiceModel RegisterResult { get; init; } = default!;

    public AuthTokenServiceModel LoginResult { get; init; } = default!;

    public int RegisterCallCount { get; private set; }

    public int AuthenticateCallCount { get; private set; }

    public Task<AccountServiceModel> RegisterAsync(RegisterAccountViewModel viewModel, CancellationToken cancellationToken = default)
    {
        RegisterCallCount++;
        return Task.FromResult(RegisterResult);
    }

    public Task<AuthTokenServiceModel> AuthenticateAsync(LoginViewModel viewModel, CancellationToken cancellationToken = default)
    {
        AuthenticateCallCount++;
        return Task.FromResult(LoginResult);
    }
}
