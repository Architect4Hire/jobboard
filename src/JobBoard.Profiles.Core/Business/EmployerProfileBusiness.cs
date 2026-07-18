using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Managers.Mappers;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Business;

/// <inheritdoc cref="IEmployerProfileBusiness"/>
public sealed class EmployerProfileBusiness : IEmployerProfileBusiness
{
    private readonly IEmployerProfileDataLayer _dataLayer;

    public EmployerProfileBusiness(IEmployerProfileDataLayer dataLayer) => _dataLayer = dataLayer;

    public async Task<EmployerProfileServiceModel?> GetAsync(Guid employerId, CancellationToken cancellationToken = default)
    {
        var profile = await _dataLayer.GetAsync(employerId, cancellationToken);
        return profile?.ToServiceModel();
    }

    public async Task<EmployerProfileServiceModel> UpsertAsync(Guid employerId, UpsertEmployerProfileViewModel viewModel, CancellationToken cancellationToken = default)
    {
        var incoming = viewModel.ToEntity(employerId);
        var saved = await _dataLayer.UpsertAsync(incoming, cancellationToken);
        return saved.ToServiceModel();
    }
}
