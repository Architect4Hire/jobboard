using JobBoard.Identity.Core.Facade;
using JobBoard.Identity.Core.Managers.Models.ServiceModels;
using JobBoard.Identity.Core.Managers.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobBoard.Identity.Controllers;

/// <summary>
/// HTTP surface for accounts. Thin by design: bind a view model, call the facade, return an
/// <see cref="ActionResult{TValue}"/> of a service model. Validation, hashing, token issuance, and data
/// access all live in the <c>.Core</c> stack. These routes are public at the gateway (you can't require
/// a token to obtain one); the token this issues is what the gateway then requires on protected routes.
/// </summary>
[ApiController]
[Route("identity")]
public sealed class IdentityController : ControllerBase
{
    private readonly IAccountFacade _facade;

    public IdentityController(IAccountFacade facade) => _facade = facade;

    /// <summary>Register a new account. Returns the created account; the client logs in to get a token.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AccountServiceModel>> Register(
        [FromBody] RegisterAccountViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var account = await _facade.RegisterAsync(viewModel, cancellationToken);
        // Accounts aren't individually retrievable (no GET-by-id), so 201 with the created account as the
        // body and the register endpoint as the location — the URL generates without a dependent route.
        return CreatedAtAction(nameof(Register), account);
    }

    /// <summary>Exchange credentials for a signed JWT bearer token.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthTokenServiceModel>> Login(
        [FromBody] LoginViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var token = await _facade.LoginAsync(viewModel, cancellationToken);
        return Ok(token);
    }
}
