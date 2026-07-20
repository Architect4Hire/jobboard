using JobBoard.Jobs.Core.Data;
using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;

namespace JobBoard.Jobs.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IJobRepository"/> for data-layer composition tests (this repo uses fakes, not
/// a mocking library). Records the sequence of calls and runs the transaction callback for real, so a
/// test can assert the operation was composed inside a transaction and in the right order.
/// </summary>
public sealed class FakeJobRepository : IJobRepository
{
    public List<string> Calls { get; } = [];

    public IReadOnlyList<JobSummaryServiceModel> ListResult { get; init; } = [];

    public Job? GetResult { get; init; }

    public Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(string? categorySlug, CancellationToken cancellationToken = default)
    {
        Calls.Add($"list:{categorySlug}");
        return Task.FromResult(ListResult);
    }

    public Task<Job?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Calls.Add($"get:{id}");
        return Task.FromResult(GetResult);
    }

    public bool CloseIfOpenResult { get; init; } = true;

    /// <summary>When set, <see cref="AddAsync"/> throws it — used to drive the duplicate-slug mapping.</summary>
    public Exception? AddError { get; init; }

    public Task<Job> AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        Calls.Add("add");
        if (AddError is not null)
        {
            throw AddError;
        }

        return Task.FromResult(job);
    }

    public Task<bool> CloseIfOpenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Calls.Add("close");
        return Task.FromResult(CloseIfOpenResult);
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
