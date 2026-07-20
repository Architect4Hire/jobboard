using JobBoard.Applications.Core.Facade;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Applications.Core.Managers.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobBoard.Applications.Controllers;

[ApiController]
[Route("applications")]
public sealed class ApplicationsController : ControllerBase
{
    private readonly IApplicationFacade _facade;

    public ApplicationsController(IApplicationFacade facade) => _facade = facade;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApplicationSummaryServiceModel>>> List(
        [FromQuery] Guid candidateId,
        CancellationToken cancellationToken)
    {
        var applications = await _facade.ListByCandidateAsync(candidateId, cancellationToken);
        return Ok(applications);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApplicationDetailServiceModel>> Get(Guid id, CancellationToken cancellationToken)
    {
        var application = await _facade.GetAsync(id, cancellationToken);
        return application is null ? NotFound() : Ok(application);
    }

    [HttpPost]
    public async Task<ActionResult<ApplicationDetailServiceModel>> Submit(
        [FromBody] SubmitApplicationViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var application = await _facade.SubmitAsync(viewModel, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = application.Id }, application);
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<ApplicationDetailServiceModel>> Withdraw(Guid id, CancellationToken cancellationToken)
    {
        var application = await _facade.WithdrawAsync(id, cancellationToken);
        return Ok(application);
    }

    [HttpPost("{id:guid}/advance")]
    public async Task<ActionResult<ApplicationDetailServiceModel>> Advance(
        Guid id,
        [FromBody] AdvanceApplicationStatusViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var application = await _facade.AdvanceAsync(id, viewModel, cancellationToken);
        return Ok(application);
    }
}
