using JobBoard.Identity.Core.Data;
using JobBoard.Identity.Core.Managers.Mappers;
using JobBoard.Identity.Core.Managers.Models.ServiceModels;
using JobBoard.Identity.Core.Managers.Models.ViewModels;
using JobBoard.Identity.Core.Security;
using JobBoard.Shared.Errors;
using Microsoft.AspNetCore.Http;

namespace JobBoard.Identity.Core.Business;

/// <inheritdoc cref="IAccountBusiness"/>
public sealed class AccountBusiness : IAccountBusiness
{
    private readonly IAccountDataLayer _dataLayer;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenIssuer _tokenIssuer;

    public AccountBusiness(IAccountDataLayer dataLayer, IPasswordHasher passwordHasher, ITokenIssuer tokenIssuer)
    {
        _dataLayer = dataLayer;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
    }

    public async Task<AccountServiceModel> RegisterAsync(RegisterAccountViewModel viewModel, CancellationToken cancellationToken = default)
    {
        var passwordHash = _passwordHasher.Hash(viewModel.Password);
        // Store email normalized so the unique index and login lookups are case/whitespace insensitive.
        var account = (viewModel with { Email = Normalize(viewModel.Email) }).ToEntity(passwordHash);

        var saved = await _dataLayer.RegisterAsync(account, cancellationToken);
        return saved.ToServiceModel();
    }

    public async Task<AuthTokenServiceModel> AuthenticateAsync(LoginViewModel viewModel, CancellationToken cancellationToken = default)
    {
        var account = await _dataLayer.GetByEmailAsync(Normalize(viewModel.Email), cancellationToken);

        // Same error for an unknown email and a wrong password — never reveal which was wrong.
        if (account is null || !_passwordHasher.Verify(account.PasswordHash, viewModel.Password))
        {
            throw new DomainException(
                "account.invalid_credentials",
                "Email or password is incorrect.",
                StatusCodes.Status401Unauthorized);
        }

        var token = _tokenIssuer.Issue(account);
        return new AuthTokenServiceModel(token.AccessToken, "Bearer", token.ExpiresAtUtc);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
