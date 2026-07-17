using JobBoard.Identity.Core.Managers.Models.ServiceModels;
using JobBoard.Identity.Core.Managers.Models.ViewModels;

namespace JobBoard.Identity.Core.Business;

/// <summary>
/// Domain rules and translation for accounts: register hashes the password, translates the view model
/// to a domain <c>Account</c>, and maps the persisted entity to a service model; authenticate verifies
/// the credential and, on success, issues the JWT. Depends only on <see cref="Data.IAccountDataLayer"/>
/// plus the password/token seams.
/// </summary>
public interface IAccountBusiness
{
    Task<AccountServiceModel> RegisterAsync(RegisterAccountViewModel viewModel, CancellationToken cancellationToken = default);

    /// <summary>Verifies credentials and returns a freshly issued token; throws 401 on any mismatch.</summary>
    Task<AuthTokenServiceModel> AuthenticateAsync(LoginViewModel viewModel, CancellationToken cancellationToken = default);
}
