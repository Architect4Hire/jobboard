using JobBoard.Profiles.Core.Facade;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobBoard.Profiles.Controllers;

/// <summary>
/// HTTP surface for candidate profiles. Thin by design: bind a view model / route value, call the facade,
/// return an <see cref="ActionResult{TValue}"/> of a service model. The owning candidate id comes from
/// the route. These routes are protected at the gateway (personal data). The résumé is a file, managed by
/// the three <c>{candidateId}/resume</c> actions rather than the JSON upsert.
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

    /// <summary>Upload (or replace) the candidate's résumé file; returns the updated profile.</summary>
    [HttpPost("{candidateId:guid}/resume")]
    [RequestSizeLimit(CandidateProfileFacade.MaxResumeBytes)]
    public async Task<ActionResult<CandidateProfileServiceModel>> UploadResume(
        Guid candidateId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest();
        }

        await using var stream = file.OpenReadStream();
        var profile = await _facade.UploadResumeAsync(
            candidateId, stream, file.Length, file.ContentType, file.FileName, cancellationToken);
        return Ok(profile);
    }

    /// <summary>Download the candidate's résumé file (streamed with its original name and content type).</summary>
    [HttpGet("{candidateId:guid}/resume")]
    public async Task<IActionResult> DownloadResume(Guid candidateId, CancellationToken cancellationToken)
    {
        var resume = await _facade.GetResumeAsync(candidateId, cancellationToken);
        return resume is null
            ? NotFound()
            : File(resume.Content, resume.ContentType, resume.FileName);
    }

    /// <summary>Remove the candidate's résumé; returns the updated profile.</summary>
    [HttpDelete("{candidateId:guid}/resume")]
    public async Task<ActionResult<CandidateProfileServiceModel>> DeleteResume(Guid candidateId, CancellationToken cancellationToken)
    {
        var profile = await _facade.DeleteResumeAsync(candidateId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }
}
