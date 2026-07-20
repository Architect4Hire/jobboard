using JobBoard.Contracts;
using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Jobs.Core.Data;

/// <inheritdoc cref="IJobDataLayer"/>
public sealed class JobDataLayer : IJobDataLayer
{
    private readonly IJobRepository _repository;
    private readonly IOutbox _outbox;

    public JobDataLayer(IJobRepository repository, IOutbox outbox)
    {
        _repository = repository;
        _outbox = outbox;
    }

    // Reads are single self-contained ops — straight pass-through, no transaction needed.
    public Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(string? categorySlug, CancellationToken cancellationToken = default) =>
        _repository.ListAsync(categorySlug, cancellationToken);

    public Task<Job?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    // Add reconciles classifications and inserts the job, then enqueues JobPosted — the writes and the
    // outbox row are one transaction, so the event ships iff the job commits.
    public async Task<Job> AddAsync(Job job, JobPosted @event, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _repository.ExecuteInTransactionAsync(
                async token =>
                {
                    var saved = await _repository.AddAsync(job, token);
                    await _outbox.EnqueueAsync(@event, token);
                    return saved;
                },
                cancellationToken);
        }
        catch (DbUpdateException ex) when (JobRepository.IsDuplicateSlugViolation(ex))
        {
            // A concurrent post created a category/tag with the same brand-new slug in the window between
            // this request's reconcile SELECT and its INSERT. Surface a retryable conflict instead of a
            // 500 — a retry re-runs the reconcile, finds the now-committed row, and reuses it.
            throw new DomainException(
                "job.classification_conflict",
                "A category or tag with the same slug was just created. Please retry.",
                StatusCodes.Status409Conflict);
        }
    }

    public Task<bool> CloseAsync(Guid id, JobClosed @event, CancellationToken cancellationToken = default) =>
        // The conditional close and the outbox write are one unit: the status UPDATE and the outbox INSERT
        // commit together (or roll back together). The event is enqueued only when a row actually flipped,
        // so a job that was already closed publishes nothing. The same @event instance is captured across
        // an execution-strategy retry, so its id is stable and a replay re-enqueues the same row (deduped
        // by id in Outbox.EnqueueAsync) rather than a duplicate.
        _repository.ExecuteInTransactionAsync(
            async token =>
            {
                var closed = await _repository.CloseIfOpenAsync(id, token);
                if (!closed)
                {
                    return false;
                }

                await _outbox.EnqueueAsync(@event, token);
                return true;
            },
            cancellationToken);
}
