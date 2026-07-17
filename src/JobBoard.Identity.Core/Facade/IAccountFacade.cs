using JobBoard.Identity.Core.Managers.Models.ServiceModels;
using JobBoard.Identity.Core.Managers.Models.ViewModels;

namespace JobBoard.Identity.Core.Facade;

/// <summary>
/// The boundary the controller calls: validates inbound view models, then delegates to
/// <see cref="Business.IAccountBusiness"/>. No mapping, EF, hashing, or token logic here.
/// </summary>
public interface IAccountFacade
{
    Task<AccountServiceModel> RegisterAsync(RegisterAccountViewModel viewModel, CancellationToken cancellationToken = default);

    Task<AuthTokenServiceModel> LoginAsync(LoginViewModel viewModel, CancellationToken cancellationToken = default);
}
