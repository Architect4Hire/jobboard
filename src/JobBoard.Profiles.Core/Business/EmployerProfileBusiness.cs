using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Managers.Mappers;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;
using JobBoard.Shared.Requests;

namespace JobBoard.Profiles.Core.Business;

/// <inheritdoc cref="IEmployerProfileBusiness"/>
public sealed class EmployerProfileBusiness : IEmployerProfileBusiness
{
    private readonly IEmployerProfileDataLayer _dataLayer;
    private readonly IRequestContext _requestContext;

    public EmployerProfileBusiness(IEmployerProfileDataLayer dataLayer, IRequestContext requestContext)
    {
        _dataLayer = dataLayer;
        _requestContext = requestContext;
    }

    public async Task<EmployerProfileServiceModel?> GetAsync(Guid employerId, CancellationToken cancellationToken = default)
    {
        var profile = await _dataLayer.GetAsync(employerId, cancellationToken);
        return profile?.ToServiceModel();
    }

    public async Task<EmployerProfileServiceModel> UpsertAsync(Guid employerId, UpsertEmployerProfileViewModel viewModel, CancellationToken cancellationToken = default)
    {
        var incoming = viewModel.ToEntity(employerId);
        // The employer edits their own company profile — the actor is the authenticated caller (ADR-0013).
        var thread = _requestContext.RootThread();
        var updated = incoming.ToProfileUpdated(thread);
        // Same write, same thread, a second fact: the state-transfer twin Applications consumes (ADR-0012).
        var changed = incoming.ToEmployerProfileChanged(thread);
        var saved = await _dataLayer.UpsertAsync(incoming, updated, changed, cancellationToken);
        return saved.ToServiceModel();
    }
}
