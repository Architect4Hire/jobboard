using FluentValidation;
using JobBoard.Identity.Core.Business;
using JobBoard.Identity.Core.Managers.Models.ServiceModels;
using JobBoard.Identity.Core.Managers.Models.ViewModels;

namespace JobBoard.Identity.Core.Facade;

/// <inheritdoc cref="IAccountFacade"/>
/// <remarks>
/// No caching here: accounts are written on register and read only to authenticate, so there is nothing
/// a read-through cache would help (and a stale credential cache would be a hazard). The facade owns the
/// validation seam and delegates.
/// </remarks>
public sealed class AccountFacade : IAccountFacade
{
    private readonly IAccountBusiness _business;
    private readonly IValidator<RegisterAccountViewModel> _registerValidator;
    private readonly IValidator<LoginViewModel> _loginValidator;

    public AccountFacade(
        IAccountBusiness business,
        IValidator<RegisterAccountViewModel> registerValidator,
        IValidator<LoginViewModel> loginValidator)
    {
        _business = business;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    public async Task<AccountServiceModel> RegisterAsync(RegisterAccountViewModel viewModel, CancellationToken cancellationToken = default)
    {
        // The global exception handler maps the thrown ValidationException to a 400 with field detail.
        await _registerValidator.ValidateAndThrowAsync(viewModel, cancellationToken);
        return await _business.RegisterAsync(viewModel, cancellationToken);
    }

    public async Task<AuthTokenServiceModel> LoginAsync(LoginViewModel viewModel, CancellationToken cancellationToken = default)
    {
        await _loginValidator.ValidateAndThrowAsync(viewModel, cancellationToken);
        return await _business.AuthenticateAsync(viewModel, cancellationToken);
    }
}
