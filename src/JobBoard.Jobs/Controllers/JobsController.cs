using JobBoard.Jobs.Core.Facade;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Jobs.Core.Managers.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobBoard.Jobs.Controllers;

/// <summary>
/// HTTP surface for job postings. Thin by design: bind a view model / route value, call the facade,
/// return an <see cref="ActionResult{TValue}"/> of a service model. No validation, caching, rules, or
/// data access — those live in the <c>.Core</c> stack.
/// </summary>
[ApiController]
[Route("jobs")]
public sealed class JobsController : ControllerBase
{
    private readonly IJobFacade _facade;

    public JobsController(IJobFacade facade) => _facade = facade;

    /// <summary>List open jobs, optionally filtered to a category slug.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JobSummaryServiceModel>>> List(
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        var jobs = await _facade.ListAsync(category, cancellationToken);
        return Ok(jobs);
    }

    /// <summary>Get one job by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobDetailServiceModel>> Get(Guid id, CancellationToken cancellationToken)
    {
        var job = await _facade.GetAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    /// <summary>Post a new job.</summary>
    [HttpPost]
    public async Task<ActionResult<JobDetailServiceModel>> Post(
        [FromBody] PostJobViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var job = await _facade.PostAsync(viewModel, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = job.Id }, job);
    }

    /// <summary>Close an open job — publishes <c>JobClosed</c> through the outbox.</summary>
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<JobDetailServiceModel>> Close(Guid id, CancellationToken cancellationToken)
    {
        var job = await _facade.CloseAsync(id, cancellationToken);
        return Ok(job);
    }
}
