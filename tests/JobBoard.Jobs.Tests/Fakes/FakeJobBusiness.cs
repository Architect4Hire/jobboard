using JobBoard.Jobs.Core.Business;
using JobBoard.Jobs.Core.Managers.Models.ServiceModels;
using JobBoard.Jobs.Core.Managers.Models.ViewModels;

namespace JobBoard.Jobs.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IJobBusiness"/> for facade tests. Records whether post was reached (so a test
/// can prove validation short-circuits before business) and returns a configured detail model.
/// </summary>
public sealed class FakeJobBusiness : IJobBusiness
{
    public JobDetailServiceModel Result { get; init; } = default!;

    public int PostCallCount { get; private set; }

    public PostJobViewModel? PostedViewModel { get; private set; }

    public Task<IReadOnlyList<JobSummaryServiceModel>> ListAsync(string? categorySlug, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<JobSummaryServiceModel>>([]);

    public Task<JobDetailServiceModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<JobDetailServiceModel?>(Result);

    public Task<JobDetailServiceModel> PostAsync(PostJobViewModel viewModel, CancellationToken cancellationToken = default)
    {
        PostCallCount++;
        PostedViewModel = viewModel;
        return Task.FromResult(Result);
    }

    public Task<JobDetailServiceModel> CloseAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result);
}
