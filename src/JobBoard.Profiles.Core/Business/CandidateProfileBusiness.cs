using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Managers.Mappers;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;

namespace JobBoard.Profiles.Core.Business;

/// <inheritdoc cref="ICandidateProfileBusiness"/>
public sealed class CandidateProfileBusiness : ICandidateProfileBusiness
{
    private readonly ICandidateProfileDataLayer _dataLayer;

    public CandidateProfileBusiness(ICandidateProfileDataLayer dataLayer) => _dataLayer = dataLayer;

    public async Task<CandidateProfileServiceModel?> GetAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var profile = await _dataLayer.GetAsync(candidateId, cancellationToken);
        return profile?.ToServiceModel();
    }

    public async Task<CandidateProfileServiceModel> UpsertAsync(Guid candidateId, UpsertCandidateProfileViewModel viewModel, CancellationToken cancellationToken = default)
    {
        var incoming = viewModel.ToEntity(candidateId);
        var saved = await _dataLayer.UpsertAsync(incoming, cancellationToken);
        return saved.ToServiceModel();
    }
}
