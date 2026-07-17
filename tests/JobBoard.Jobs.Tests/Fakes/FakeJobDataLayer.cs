using JobBoard.Contracts;
using JobBoard.Jobs.Core.Data;
using JobBoard.Jobs.Core.Managers.Models.Domain;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;

namespace JobBoard.Jobs.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IJobDataLayer"/> for business-layer tests. Returns configured values and
/// captures what business handed down — the job it tried to add, and the job + event it closed with —
/// so a test can assert the VM→domain translation and the built event without a database.
/// </summary>
public sealed class FakeJobDataLayer : IJobDataLayer
{
    public IReadOnlyList<JobSummaryServiceModel> ListResult { get; init; } = [];

    public Job? GetResult { get; set; }

    public Job? AddedJob { get; private set; }

    public Guid? ClosedId { get; private set; }

    public JobClosed? ClosedEvent { get; private set; }

    /// <summary>What <see cref="CloseAsync"/> reports — set false to simulate losing a concurrent close.</summary>
    public bool CloseResult { get; set; } = true;

    public Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(string? categorySlug, CancellationToken cancellationToken = default) =>
        Task.FromResult(ListResult);

    public Task<Job?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetResult);

    public Task<Job> AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        AddedJob = job;
        return Task.FromResult(job);
    }

    public Task<bool> CloseAsync(Guid id, JobClosed @event, CancellationToken cancellationToken = default)
    {
        ClosedId = id;
        ClosedEvent = @event;
        return Task.FromResult(CloseResult);
    }
}
