using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Managers.Mappers;

/// <summary>
/// The two mapping seams the employer business layer owns: <b>ViewModel → Domain</b> (upsert, taking the
/// owner id from the route) and <b>Domain → ServiceModel</b> (every response).
/// </summary>
public static class EmployerProfileMappers
{
    public static EmployerProfile ToEntity(this UpsertEmployerProfileViewModel vm, Guid employerId) => new()
    {
        Id = employerId,
        CompanyName = vm.CompanyName,
        Website = vm.Website,
        Description = vm.Description,
        UpdatedOnUtc = DateTime.UtcNow,
    };

    public static EmployerProfileServiceModel ToServiceModel(this EmployerProfile profile) => new(
        profile.Id,
        profile.CompanyName,
        profile.Website,
        profile.Description,
        profile.UpdatedOnUtc);
}
