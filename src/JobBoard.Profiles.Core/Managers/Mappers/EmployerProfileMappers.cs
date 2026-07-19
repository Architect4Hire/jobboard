using JobBoard.Contracts;
using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;
using JobBoard.Shared.Requests;

namespace JobBoard.Profiles.Core.Managers.Mappers;

/// <summary>
/// The mapping seams the employer business layer owns: <b>ViewModel → Domain</b> (upsert, taking the owner
/// id from the route), <b>Domain → ServiceModel</b> (every response), and <b>Domain → integration event</b>
/// (the <see cref="ProfileUpdated"/> audit fact — ids + type + timestamp only, no company field values).
/// </summary>
public static class EmployerProfileMappers
{
    /// <summary>
    /// Builds the <see cref="ProfileUpdated"/> fact for an employer profile that has just been written,
    /// stamping a fresh event id and the audit <paramref name="thread"/> (ADR-0013). The subject is the
    /// profile id (== the owning employer's account id) and the occurred-at is the row's update time.
    /// </summary>
    public static ProfileUpdated ToProfileUpdated(this EmployerProfile profile, AuditThread thread) =>
        new(Guid.NewGuid(), profile.Id, "Employer", profile.UpdatedOnUtc)
        {
            CorrelationId = thread.CorrelationId,
            CausationId = thread.CausationId,
            ActorId = thread.ActorId,
        };

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
