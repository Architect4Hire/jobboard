using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Contracts;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Applications.Core.Data;

/// <summary>
/// Implementation of <see cref="IApplicationDataLayer"/>. Each write that emits an event wraps the whole
/// operation — the domain change <i>and</i> the outbox row — in <c>ExecuteInTransactionAsync</c>, so the
/// event ships iff the change commits. The consumer path additionally checks and stamps the inbox inside
/// the same transaction, making a redelivery a no-op.
/// </summary>
public sealed class ApplicationDataLayer : IApplicationDataLayer
{
    private readonly IApplicationRepository _repository;
    private readonly IOutbox _outbox;
    private readonly IInbox _inbox;

    public ApplicationDataLayer(IApplicationRepository repository, IOutbox outbox, IInbox inbox)
    {
        _repository = repository;
        _outbox = outbox;
        _inbox = inbox;
    }

    // Reads pass straight through — no transaction.
    public Task<IReadOnlyList<ApplicationSummaryServiceModel>> ListByCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default) =>
        _repository.ListByCandidateAsync(candidateId, cancellationToken);

    public Task<Application?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    public async Task<Application> SubmitAsync(
        Application application,
        ApplicationSubmitted @event,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _repository.ExecuteInTransactionAsync(
                async token =>
                {
                    var saved = await _repository.AddAsync(application, token);
                    // Same DbContext, same transaction: the event ships iff this row commits.
                    await _outbox.EnqueueAsync(@event, token);
                    return saved;
                },
                cancellationToken);
        }
        catch (DbUpdateException ex) when (ApplicationRepository.IsDuplicateApplicationViolation(ex))
        {
            throw new DomainException(
                "application.duplicate",
                $"Candidate '{application.CandidateId}' has already applied to job '{application.JobId}'.",
                StatusCodes.Status409Conflict);
        }
    }

    public Task<bool> WithdrawAsync(
        Guid id,
        ApplicationStatusChanged @event,
        CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(
            async token =>
            {
                var changed = await _repository.WithdrawIfActiveAsync(id, token);
                if (!changed)
                {
                    return false;
                }

                await _outbox.EnqueueAsync(@event, token);
                return true;
            },
            cancellationToken);

    public Task<bool> AdvanceAsync(
        Guid id,
        ApplicationStatus expected,
        ApplicationStatus target,
        ApplicationStatusChanged @event,
        CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(
            async token =>
            {
                var changed = await _repository.AdvanceIfInStatusAsync(id, expected, target, token);
                if (!changed)
                {
                    return false;
                }

                await _outbox.EnqueueAsync(@event, token);
                return true;
            },
            cancellationToken);

    public Task<int> CloseOpenApplicationsForJobAsync(
        Guid jobId,
        Guid messageId,
        ApplicationStatus target,
        Func<Application, ApplicationStatusChanged> buildEvent,
        CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(
            async token =>
            {
                // Idempotency: a redelivery of the same JobClosed message finds its inbox row and no-ops.
                if (await _inbox.HasProcessedAsync(messageId, token))
                {
                    return 0;
                }

                // Untracked snapshot of pre-close state — each app's Status is the "from" of its event.
                var active = await _repository.GetActiveByJobAsync(jobId, token);

                // Authoritative close: one conditional UPDATE, re-evaluated at write time.
                await _repository.CloseActiveByJobAsync(jobId, target, token);

                // Publish a fact only for rows this operation actually moved to target. The conditional
                // UPDATE re-evaluates "active" at write time, so a row transitioned concurrently (e.g.
                // withdrawn) between the snapshot and the close is left alone — and must get no event.
                var closedIds = await _repository.GetIdsInStatusAsync(
                    active.Select(a => a.Id).ToList(), target, token);

                foreach (var application in active)
                {
                    if (closedIds.Contains(application.Id))
                    {
                        await _outbox.EnqueueAsync(buildEvent(application), token);
                    }
                }

                await _inbox.MarkProcessedAsync(messageId, token);
                return closedIds.Count;
            },
            cancellationToken);

    public Task UpsertJobReferenceAsync(
        Guid jobId,
        Guid messageId,
        string title,
        Guid employerId,
        CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(
            async token =>
            {
                if (await _inbox.HasProcessedAsync(messageId, token))
                {
                    return;
                }

                await _repository.UpsertJobReferenceAsync(jobId, title, employerId, token);
                await _inbox.MarkProcessedAsync(messageId, token);
            },
            cancellationToken);

    public Task UpsertEmployerReferenceAsync(
        Guid employerId,
        Guid messageId,
        string companyName,
        CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(
            async token =>
            {
                if (await _inbox.HasProcessedAsync(messageId, token))
                {
                    return;
                }

                await _repository.UpsertEmployerReferenceAsync(employerId, companyName, token);
                await _inbox.MarkProcessedAsync(messageId, token);
            },
            cancellationToken);

    // Reads pass straight through — no transaction.
    public Task<IReadOnlyList<ApplicationHistoryServiceModel>> ListMineAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default) =>
        _repository.ListMineAsync(candidateId, cancellationToken);
}
