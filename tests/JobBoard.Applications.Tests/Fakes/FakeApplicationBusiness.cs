using JobBoard.Applications.Core.Business;
using JobBoard.Applications.Core.Managers.Models.ServiceModels;
using JobBoard.Applications.Core.Managers.Models.ViewModels;
using JobBoard.Contracts;

namespace JobBoard.Applications.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IApplicationBusiness"/> for facade tests. Records whether each write was reached
/// (so a test can prove validation short-circuits before business) and returns a configured detail model.
/// </summary>
public sealed class FakeApplicationBusiness : IApplicationBusiness
{
    public ApplicationDetailServiceModel Result { get; init; } = default!;

    public int SubmitCallCount { get; private set; }

    public int AdvanceCallCount { get; private set; }

    public SubmitApplicationViewModel? SubmittedViewModel { get; private set; }

    public AdvanceApplicationStatusViewModel? AdvancedViewModel { get; private set; }

    public Task<IReadOnlyList<ApplicationSummaryServiceModel>> ListByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ApplicationSummaryServiceModel>>([]);

    public Task<ApplicationDetailServiceModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<ApplicationDetailServiceModel?>(Result);

    public Task<ApplicationDetailServiceModel> SubmitAsync(SubmitApplicationViewModel viewModel, CancellationToken cancellationToken = default)
    {
        SubmitCallCount++;
        SubmittedViewModel = viewModel;
        return Task.FromResult(Result);
    }

    public Task<ApplicationDetailServiceModel> WithdrawAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result);

    public Task<ApplicationDetailServiceModel> AdvanceAsync(Guid id, AdvanceApplicationStatusViewModel viewModel, CancellationToken cancellationToken = default)
    {
        AdvanceCallCount++;
        AdvancedViewModel = viewModel;
        return Task.FromResult(Result);
    }

    public Task HandleJobClosedAsync(JobClosed @event, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public IReadOnlyList<ApplicationHistoryServiceModel> MineResult { get; init; } = [];

    public Task HandleJobPostedAsync(JobPosted @event, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task HandleEmployerProfileChangedAsync(EmployerProfileChanged @event, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<ApplicationHistoryServiceModel>> ListMineAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(MineResult);
}
