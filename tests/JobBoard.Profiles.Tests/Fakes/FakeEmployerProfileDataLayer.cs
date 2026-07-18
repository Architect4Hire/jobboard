using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Managers.Models.Domain;

namespace JobBoard.Profiles.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IEmployerProfileDataLayer"/> for business-layer tests. Returns configured
/// values and captures the entity business handed down.
/// </summary>
public sealed class FakeEmployerProfileDataLayer : IEmployerProfileDataLayer
{
    public EmployerProfile? GetResult { get; set; }

    public EmployerProfile? Upserted { get; private set; }

    public Task<EmployerProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetResult);

    public Task<EmployerProfile> UpsertAsync(EmployerProfile incoming, CancellationToken cancellationToken = default)
    {
        Upserted = incoming;
        return Task.FromResult(incoming);
    }
}
