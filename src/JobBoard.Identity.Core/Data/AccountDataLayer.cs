using JobBoard.Contracts;
using JobBoard.Identity.Core.Managers.Models.Domain;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Identity.Core.Data;

/// <inheritdoc cref="IAccountDataLayer"/>
public sealed class AccountDataLayer : IAccountDataLayer
{
    private readonly IAccountRepository _repository;
    private readonly IOutbox _outbox;

    public AccountDataLayer(IAccountRepository repository, IOutbox outbox)
    {
        _repository = repository;
        _outbox = outbox;
    }

    // A single self-contained read — straight pass-through, no transaction needed.
    public Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _repository.GetByEmailAsync(email, cancellationToken);

    public async Task<Account> RegisterAsync(Account account, AccountCreated created, CancellationToken cancellationToken = default)
    {
        try
        {
            // The insert and the outbox row are one transaction, so AccountCreated ships iff the account
            // commits. The repository only stages the insert; the transaction is what SaveChanges/commits it.
            return await _repository.ExecuteInTransactionAsync(
                async token =>
                {
                    var saved = await _repository.AddAsync(account, token);
                    await _outbox.EnqueueAsync(created, token);
                    return saved;
                },
                cancellationToken);
        }
        catch (DbUpdateException ex) when (AccountRepository.IsDuplicateEmailViolation(ex))
        {
            // The unique index is the authoritative guard: a concurrent registration inserted this email
            // between our read and our insert. Surface a conflict rather than a 500.
            throw new DomainException(
                "account.email_taken",
                "An account with this email already exists.",
                StatusCodes.Status409Conflict);
        }
    }

    // Login changes no domain state, so the audit event is the only write: the outbox row commits through a
    // transaction (the same seam every publish uses), then the dispatcher relays it. Enqueue is id-deduped,
    // so an execution-strategy retry re-enqueues the same row rather than a duplicate.
    public Task RecordLoginAsync(LoggedIn loggedIn, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(
            token => _outbox.EnqueueAsync(loggedIn, token),
            cancellationToken);

    public Task RecordLoginFailedAsync(LoginFailed loginFailed, CancellationToken cancellationToken = default) =>
        _repository.ExecuteInTransactionAsync(
            token => _outbox.EnqueueAsync(loginFailed, token),
            cancellationToken);
}
