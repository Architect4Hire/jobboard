using JobBoard.Profiles.Core.Facade;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobBoard.Profiles.Controllers;

/// <summary>
/// HTTP surface for employer company profiles. Thin by design. The owning employer id comes from the
/// route. At the gateway, <c>GET</c> is public (candidates browsing employers) while <c>PUT</c> is
/// protected.
/// </summary>
[ApiController]
[Route("profiles/employers")]
public sealed class EmployerProfilesController : ControllerBase
{
    private readonly IEmployerProfileFacade _facade;

    public EmployerProfilesController(IEmployerProfileFacade facade) => _facade = facade;

    /// <summary>Get an employer's public company profile by employer id.</summary>
    [HttpGet("{employerId:guid}")]
    public async Task<ActionResult<EmployerProfileServiceModel>> Get(Guid employerId, CancellationToken cancellationToken)
    {
        var profile = await _facade.GetAsync(employerId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>Create or replace an employer's company profile (idempotent upsert by employer id).</summary>
    [HttpPut("{employerId:guid}")]
    public async Task<ActionResult<EmployerProfileServiceModel>> Upsert(
        Guid employerId,
        [FromBody] UpsertEmployerProfileViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var profile = await _facade.UpsertAsync(employerId, viewModel, cancellationToken);
        return Ok(profile);
    }
}
