using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Facade;

/// <summary>
/// The boundary the employer-profile controller calls: validates the upsert view model, then delegates
/// to <see cref="Business.IEmployerProfileBusiness"/>. No mapping, EF, or caching here.
/// </summary>
public interface IEmployerProfileFacade
{
    Task<EmployerProfileServiceModel?> GetAsync(Guid employerId, CancellationToken cancellationToken = default);

    Task<EmployerProfileServiceModel> UpsertAsync(Guid employerId, UpsertEmployerProfileViewModel viewModel, CancellationToken cancellationToken = default);
}
