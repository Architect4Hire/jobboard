using JobBoard.Profiles.Core.Facade;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobBoard.Profiles.Controllers;

/// <summary>
/// HTTP surface for candidate profiles. Thin by design: bind a view model / route value, call the facade,
/// return an <see cref="ActionResult{TValue}"/> of a service model. The owning candidate id comes from
/// the route. These routes are protected at the gateway (personal data).
/// </summary>
[ApiController]
[Route("profiles/candidates")]
public sealed class CandidateProfilesController : ControllerBase
{
    private readonly ICandidateProfileFacade _facade;

    public CandidateProfilesController(ICandidateProfileFacade facade) => _facade = facade;

    /// <summary>Get a candidate's profile by candidate id.</summary>
    [HttpGet("{candidateId:guid}")]
    public async Task<ActionResult<CandidateProfileServiceModel>> Get(Guid candidateId, CancellationToken cancellationToken)
    {
        var profile = await _facade.GetAsync(candidateId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>Create or replace a candidate's profile (idempotent upsert by candidate id).</summary>
    [HttpPut("{candidateId:guid}")]
    public async Task<ActionResult<CandidateProfileServiceModel>> Upsert(
        Guid candidateId,
        [FromBody] UpsertCandidateProfileViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var profile = await _facade.UpsertAsync(candidateId, viewModel, cancellationToken);
        return Ok(profile);
    }
}
