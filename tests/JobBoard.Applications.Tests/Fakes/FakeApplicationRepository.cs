using JobBoard.Applications.Core.Data;
using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;

namespace JobBoard.Applications.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IApplicationRepository"/> for data-layer composition tests (this repo uses
/// fakes, not a mocking library). Records the sequence of calls and runs the transaction callback for
/// real, so a test can assert the operation was composed inside a transaction and in the right order.
/// </summary>
public sealed class FakeApplicationRepository : IApplicationRepository
{
    public List<string> Calls { get; } = [];

    public IReadOnlyList<ApplicationSummaryServiceModel> ListResult { get; init; } = [];

    public Application? GetResult { get; init; }

    public IReadOnlyList<Application> ActiveResult { get; init; } = [];

    public bool WithdrawResult { get; init; } = true;

    public bool AdvanceResult { get; init; } = true;

    public int CloseActiveResult { get; init; }

    /// <summary>
    /// When set, only these ids count as "actually closed" by <see cref="GetIdsInStatusAsync"/> (simulating a
    /// row that a concurrent transition kept out of the bulk close). When null, every queried id is reported
    /// closed — the no-race default.
    /// </summary>
    public IReadOnlySet<Guid>? ClosedIds { get; init; }

    /// <summary>When set, <see cref="AddAsync"/> throws it — used to drive the duplicate-application mapping.</summary>
    public Exception? AddError { get; init; }

    public List<(Guid JobId, string Title, Guid EmployerId)> UpsertedJobReferences { get; } = [];

    public List<(Guid EmployerId, string CompanyName)> UpsertedEmployerReferences { get; } = [];

    public IReadOnlyList<ApplicationHistoryServiceModel> MineResult { get; init; } = [];

    public Task<IReadOnlyList<ApplicationSummaryServiceModel>> ListByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        Calls.Add($"list:{candidateId}");
        return Task.FromResult(ListResult);
    }

    public Task<Application?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Calls.Add($"get:{id}");
        return Task.FromResult(GetResult);
    }

    public Task<IReadOnlyList<Application>> GetActiveByJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        Calls.Add("getActive");
        return Task.FromResult(ActiveResult);
    }

    public Task<Application> AddAsync(Application application, CancellationToken cancellationToken = default)
    {
        Calls.Add("add");
        if (AddError is not null)
        {
            throw AddError;
        }

        return Task.FromResult(application);
    }

    public Task<bool> WithdrawIfActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Calls.Add("withdraw");
        return Task.FromResult(WithdrawResult);
    }

    public Task<bool> AdvanceIfInStatusAsync(Guid id, ApplicationStatus expected, ApplicationStatus target, CancellationToken cancellationToken = default)
    {
        Calls.Add("advance");
        return Task.FromResult(AdvanceResult);
    }

    public Task<int> CloseActiveByJobAsync(Guid jobId, ApplicationStatus target, CancellationToken cancellationToken = default)
    {
        Calls.Add("closeActive");
        return Task.FromResult(CloseActiveResult);
    }

    public Task<IReadOnlySet<Guid>> GetIdsInStatusAsync(IReadOnlyCollection<Guid> ids, ApplicationStatus status, CancellationToken cancellationToken = default)
    {
        Calls.Add("closedIds");
        IReadOnlySet<Guid> result = ClosedIds is null
            ? ids.ToHashSet()
            : ids.Where(ClosedIds.Contains).ToHashSet();
        return Task.FromResult(result);
    }

    public Task UpsertJobReferenceAsync(Guid jobId, string title, Guid employerId, CancellationToken cancellationToken = default)
    {
        Calls.Add("upsertJobReference");
        UpsertedJobReferences.Add((jobId, title, employerId));
        return Task.CompletedTask;
    }

    public Task UpsertEmployerReferenceAsync(Guid employerId, string companyName, CancellationToken cancellationToken = default)
    {
        Calls.Add("upsertEmployerReference");
        UpsertedEmployerReferences.Add((employerId, companyName));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ApplicationHistoryServiceModel>> ListMineAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        Calls.Add($"listMine:{candidateId}");
        return Task.FromResult(MineResult);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        Calls.Add("tx:begin");
        var result = await operation(cancellationToken);
        Calls.Add("tx:commit");
        return result;
    }

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync<object?>(async token =>
        {
            await operation(token);
            return null;
        }, cancellationToken);
}
