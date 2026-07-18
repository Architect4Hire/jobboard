using JobBoard.Profiles.Core.Business;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IEmployerProfileBusiness"/> for facade tests. Records whether upsert was
/// reached and returns a configured result.
/// </summary>
public sealed class FakeEmployerProfileBusiness : IEmployerProfileBusiness
{
    public EmployerProfileServiceModel UpsertResult { get; init; } = default!;

    public int UpsertCallCount { get; private set; }

    public Task<EmployerProfileServiceModel?> GetAsync(Guid employerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<EmployerProfileServiceModel?>(UpsertResult);

    public Task<EmployerProfileServiceModel> UpsertAsync(Guid employerId, UpsertEmployerProfileViewModel viewModel, CancellationToken cancellationToken = default)
    {
        UpsertCallCount++;
        return Task.FromResult(UpsertResult);
    }
}
