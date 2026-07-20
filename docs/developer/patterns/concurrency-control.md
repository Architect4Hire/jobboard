# Concurrency Control

*Two requests can pass the same read-side check before either commits — so the write, never the read,
has to be the authoritative guard. Two recurring shapes: the conditional-write state transition, and the
get-or-create race.*

## The problem this solves

`GetAsync` then `if (job.Status == Open)` then `SaveChanges` looks safe in isolation, but between the
read and the save another request can run the exact same sequence. Both see `Open`, both proceed, and
whichever commits second either double-applies a transition or silently overwrites the first one's
result. A rule "enforced" only against a loaded entity is not actually enforced — it's a race with two
winners. Both patterns below move the check into the database engine itself, where only one concurrent
writer can ever win.

## How it works here

### Shape 1 — conditional-write state transition

A status change (close a job, withdraw/advance an application) is written as a single `UPDATE ... WHERE
Id = @id AND Status = @expected`, via EF's `ExecuteUpdateAsync` — never a load-mutate-save. See
[`JobRepository.CloseIfOpenAsync`](../../../src/JobBoard.Jobs.Core/Data/JobRepository.cs):

```csharp
public async Task<bool> CloseIfOpenAsync(Guid id, CancellationToken cancellationToken = default)
{
    var affected = await Context.Jobs
        .Where(j => j.Id == id && j.Status == JobStatus.Open)
        .ExecuteUpdateAsync(setters => setters.SetProperty(j => j.Status, JobStatus.Closed), cancellationToken);
    return affected > 0;
}
```

Two concurrent closes can both pass a read-side "is it open?" check, but only the first `UPDATE`
actually matches `Status = Open` — by the time the second one runs, the row's status has already
changed, so it affects **zero rows**. `affected > 0` is the only thing business trusts; see
[`JobBusiness.CloseAsync`](../../../src/JobBoard.Jobs.Core/Business/JobBusiness.cs):

```csharp
var didClose = await _dataLayer.CloseAsync(job.Id, closed, cancellationToken);
if (!didClose)
{
    // A concurrent close won — this one is the conflict. No event was published for it.
    throw new DomainException("job.not_open", $"Job '{id}' is not open and cannot be closed.");
}
```

The earlier `job.Status != JobStatus.Open` check in the same method (against the entity loaded a few
lines up) is only a **fast path** — a cheap 404/409 for the obviously-already-closed case. It is not the
guard; the conditional `UPDATE` is. Publish is tied to the same authoritative signal, in
[`JobDataLayer.CloseAsync`](../../../src/JobBoard.Jobs.Core/Data/JobDataLayer.cs): the outbox enqueue
only happens *inside* the `if (closed)` branch, so the loser of the race publishes **no event** — there
is no `JobClosed` for a close that didn't actually happen.

Applications' [`ApplicationRepository`](../../../src/JobBoard.Applications.Core/Data/ApplicationRepository.cs)
uses the identical shape for `WithdrawIfActiveAsync` and `AdvanceIfInStatusAsync` — the `expected` status
is a parameter, so one method serves every transition in the application lifecycle. The
[`ApplicationDataLayer.CloseOpenApplicationsForJobAsync`](../../../src/JobBoard.Applications.Core/Data/ApplicationDataLayer.cs)
consumer (reacting to `JobClosed`) takes this one step further: it snapshots which applications were
active *before* the close, runs the conditional close, then re-queries which of those specific ids
actually landed in the target status — a row a different request withdrew in the same window is
correctly excluded and gets no `ApplicationStatusChanged` from this operation.

### Shape 2 — get-or-create under a unique constraint

Resolving categories/tags by slug (create if missing) is a `SELECT`-then-maybe-`INSERT` — and that gap
is exactly where two concurrent posts can both decide a slug is new and both try to insert it. Rather
than serialize with a lock, the repository lets the database's unique index be the referee and the data
layer turns the resulting violation into a retryable response. The classifier lives in the repository
(it owns provider-specific knowledge), in [`JobRepository.IsDuplicateSlugViolation`](../../../src/JobBoard.Jobs.Core/Data/JobRepository.cs):

```csharp
public static bool IsDuplicateSlugViolation(DbUpdateException exception) =>
    exception.InnerException switch
    {
        PostgresException pg => pg.SqlState == PostgresErrorCodes.UniqueViolation
            && (pg.ConstraintName?.Contains("Slug", StringComparison.OrdinalIgnoreCase) ?? false),
        { } inner => inner.Message.Contains("Slug", ...) && inner.Message.Contains("unique", ...),
        _ => false,
    };
```

The `catch` lives one layer up, in the data layer — because the violation surfaces from `SaveChanges`
*inside* `ExecuteInTransactionAsync`, and the data layer is what owns that transaction (see
[`JobDataLayer.AddAsync`](../../../src/JobBoard.Jobs.Core/Data/JobDataLayer.cs)):

```csharp
catch (DbUpdateException ex) when (JobRepository.IsDuplicateSlugViolation(ex))
{
    throw new DomainException("job.classification_conflict",
        "A category or tag with the same slug was just created. Please retry.",
        StatusCodes.Status409Conflict);
}
```

A `409` here is *retryable*: the client retries, the reconcile `SELECT` now finds the row the other
request committed, and it's reused instead of inserted again.
[`ApplicationRepository.IsDuplicateApplicationViolation`](../../../src/JobBoard.Applications.Core/Data/ApplicationRepository.cs)
+ [`ApplicationDataLayer.SubmitAsync`](../../../src/JobBoard.Applications.Core/Data/ApplicationDataLayer.cs)
apply the identical shape to the unique `(CandidateId, JobId)` constraint — two concurrent "apply to this
job" clicks from the same candidate can't both succeed.

## Why

Neither shape has its own ADR — it's a consequence of [ADR-0005](../../adr/0005-thin-host-core-layered-library.md)'s
repository/data-layer split (the repository owns the SQL shape; the data layer owns the transaction and
the exception→conflict mapping) applied consistently everywhere a race is possible.

## Pitfalls / rules to respect

- **A status check against a loaded entity is a fast path, never the guard.** The authoritative check is
  always a conditional write with `0 rows affected` meaning "someone else got here first."
- **A losing conditional write publishes no event.** Tie the outbox enqueue to the same boolean the
  conditional write returned — never to the pre-write entity state.
- **The repository classifies; the data layer catches.** The classifier only inspects the exception
  (provider knowledge); the `catch` belongs in the data layer because that's where the transaction (and
  thus the `SaveChanges` that can throw) is owned.
- **A get-or-create conflict is retryable (409), not a 500.** The client's retry is what actually
  resolves it — the row the race created is already committed by the time the retry runs.

See the "Concurrency: guard the write, not the read" section of
[Adding an Endpoint by Hand](../adding-an-endpoint-manually.md#concurrency-guard-the-write-not-the-read)
for this same material framed as a build-order step.

## Reference map

| Shape | Real file |
| --- | --- |
| Conditional close | [`JobRepository.cs`](../../../src/JobBoard.Jobs.Core/Data/JobRepository.cs) (`CloseIfOpenAsync`) |
| Conditional status transitions | [`ApplicationRepository.cs`](../../../src/JobBoard.Applications.Core/Data/ApplicationRepository.cs) (`WithdrawIfActiveAsync`, `AdvanceIfInStatusAsync`, `CloseActiveByJobAsync`) |
| Tying publish to the authoritative result | [`JobDataLayer.cs`](../../../src/JobBoard.Jobs.Core/Data/JobDataLayer.cs) · [`ApplicationDataLayer.cs`](../../../src/JobBoard.Applications.Core/Data/ApplicationDataLayer.cs) |
| Get-or-create unique-violation retry | [`JobRepository.cs`](../../../src/JobBoard.Jobs.Core/Data/JobRepository.cs) (`IsDuplicateSlugViolation`) · [`ApplicationRepository.cs`](../../../src/JobBoard.Applications.Core/Data/ApplicationRepository.cs) (`IsDuplicateApplicationViolation`) |
