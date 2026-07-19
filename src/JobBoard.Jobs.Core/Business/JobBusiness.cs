using JobBoard.Jobs.Core.Data;
using JobBoard.Jobs.Core.Managers.Mappers;
using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Jobs.Core.Managers.Models.ViewModels;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Requests;
using Microsoft.AspNetCore.Http;

namespace JobBoard.Jobs.Core.Business;

/// <inheritdoc cref="IJobBusiness"/>
public sealed class JobBusiness : IJobBusiness
{
    private readonly IJobDataLayer _dataLayer;
    private readonly IRequestContext _requestContext;

    public JobBusiness(IJobDataLayer dataLayer, IRequestContext requestContext)
    {
        _dataLayer = dataLayer;
        _requestContext = requestContext;
    }

    public Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(string? categorySlug, CancellationToken cancellationToken = default) =>
        _dataLayer.ListAsync(categorySlug, cancellationToken);

    public async Task<JobDetailServiceModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _dataLayer.GetAsync(id, cancellationToken);
        return job?.ToDetailServiceModel();
    }

    public async Task<JobDetailServiceModel> PostAsync(PostJobViewModel viewModel, CancellationToken cancellationToken = default)
    {
        var job = viewModel.ToEntity();
        // A post is a fact other services care about (Notifications logs it): build JobPosted and hand it
        // to the data layer, which enqueues it in the same transaction as the insert. The post is the root
        // of its request thread, so the event carries the request's own correlation/actor (ADR-0013).
        var posted = job.ToJobPosted(_requestContext.RootThread());
        var saved = await _dataLayer.AddAsync(job, posted, cancellationToken);
        return saved.ToDetailServiceModel();
    }

    public async Task<JobDetailServiceModel> CloseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _dataLayer.GetAsync(id, cancellationToken)
            ?? throw new DomainException("job.not_found", $"Job '{id}' was not found.", StatusCodes.Status404NotFound);

        if (job.Status != JobStatus.Open)
        {
            // Domain rule, fast path: only an open posting can be closed. A repeat close (or closing a
            // draft) is a conflict, not a not-found — and it must emit no event.
            throw new DomainException("job.not_open", $"Job '{id}' is not open and cannot be closed.");
        }

        var closed = job.ToJobClosed(_requestContext.RootThread());

        // The data layer's conditional close is the authoritative guard: if it reports no row flipped, a
        // concurrent request closed this job first, so nothing was published and this one is the conflict.
        var didClose = await _dataLayer.CloseAsync(job.Id, closed, cancellationToken);
        if (!didClose)
        {
            throw new DomainException("job.not_open", $"Job '{id}' is not open and cannot be closed.");
        }

        job.Status = JobStatus.Closed; // reflect the committed state in the response (the loaded copy is untracked)
        return job.ToDetailServiceModel();
    }
}
