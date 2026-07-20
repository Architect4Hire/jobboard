using JobBoard.Identity.Core.Data;
using JobBoard.Identity.Core.Managers.Mappers;
using JobBoard.Identity.Core.Managers.Models.ServiceModels;
using JobBoard.Identity.Core.Managers.Models.ViewModels;
using JobBoard.Identity.Core.Security;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Requests;
using Microsoft.AspNetCore.Http;

namespace JobBoard.Identity.Core.Business;

/// <inheritdoc cref="IAccountBusiness"/>
public sealed class AccountBusiness : IAccountBusiness
{
    private readonly IAccountDataLayer _dataLayer;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenIssuer _tokenIssuer;
    private readonly IRequestContext _requestContext;

    public AccountBusiness(
        IAccountDataLayer dataLayer,
        IPasswordHasher passwordHasher,
        ITokenIssuer tokenIssuer,
        IRequestContext requestContext)
    {
        _dataLayer = dataLayer;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _requestContext = requestContext;
    }

    public async Task<AccountServiceModel> RegisterAsync(RegisterAccountViewModel viewModel, CancellationToken cancellationToken = default)
    {
        var passwordHash = _passwordHasher.Hash(viewModel.Password);
        // Store email normalized so the unique index and login lookups are case/whitespace insensitive.
        var account = (viewModel with { Email = Normalize(viewModel.Email) }).ToEntity(passwordHash);

        // Registration runs before any token exists, so the edge projects no actor: the account is its own
        // actor (self-originated). The event ships iff the insert commits (same transaction, in the data layer).
        var created = account.ToAccountCreated(_requestContext.SelfOriginatedThread(account.Id));

        var saved = await _dataLayer.RegisterAsync(account, created, cancellationToken);
        return saved.ToServiceModel();
    }

    public async Task<AuthTokenServiceModel> AuthenticateAsync(LoginViewModel viewModel, CancellationToken cancellationToken = default)
    {
        var email = Normalize(viewModel.Email);
        var account = await _dataLayer.GetByEmailAsync(email, cancellationToken);

        // Same error for an unknown email and a wrong password — never reveal which was wrong.
        if (account is null || !_passwordHasher.Verify(account.PasswordHash, viewModel.Password))
        {
            // Record the failed attempt in the trail (uniform reason, no account id — see LoginFailed), then
            // surface the 401. The event commits through its own transaction before we throw.
            var failed = AccountMappers.ToLoginFailed(email, _requestContext.RootThread());
            await _dataLayer.RecordLoginFailedAsync(failed, cancellationToken);

            throw new DomainException(
                "account.invalid_credentials",
                "Email or password is incorrect.",
                StatusCodes.Status401Unauthorized);
        }

        // Login persists no domain state, so the LoggedIn fact is the whole write: self-originated (the
        // account is its own actor), committed through the outbox before the token is handed back.
        var loggedIn = account.ToLoggedIn(_requestContext.SelfOriginatedThread(account.Id));
        await _dataLayer.RecordLoginAsync(loggedIn, cancellationToken);

        var token = _tokenIssuer.Issue(account);
        return new AuthTokenServiceModel(token.AccessToken, "Bearer", token.ExpiresAtUtc);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
