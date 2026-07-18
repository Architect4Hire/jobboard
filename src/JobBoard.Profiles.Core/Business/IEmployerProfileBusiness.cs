using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Business;

/// <summary>
/// Translation for employer profiles: read maps entity → service model; upsert translates the view model
/// (plus the route-supplied owner id) → domain entity and maps the persisted entity → service model.
/// Depends only on <see cref="Data.IEmployerProfileDataLayer"/>.
/// </summary>
public interface IEmployerProfileBusiness
{
    Task<EmployerProfileServiceModel?> GetAsync(Guid employerId, CancellationToken cancellationToken = default);

    Task<EmployerProfileServiceModel> UpsertAsync(Guid employerId, UpsertEmployerProfileViewModel viewModel, CancellationToken cancellationToken = default);
}
