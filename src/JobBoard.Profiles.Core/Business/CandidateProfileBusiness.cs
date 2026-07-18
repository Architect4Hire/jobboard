using JobBoard.Profiles.Core.Data;
using JobBoard.Profiles.Core.Managers.Mappers;
using JobBoard.Profiles.Core.Managers.Models.Domain;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;
using JobBoard.Profiles.Core.Storage;
using JobBoard.Shared.Errors;
using Microsoft.AspNetCore.Http;

namespace JobBoard.Profiles.Core.Business;

/// <inheritdoc cref="ICandidateProfileBusiness"/>
public sealed class CandidateProfileBusiness : ICandidateProfileBusiness
{
    private readonly ICandidateProfileDataLayer _dataLayer;
    private readonly IResumeStorage _resumeStorage;

    public CandidateProfileBusiness(ICandidateProfileDataLayer dataLayer, IResumeStorage resumeStorage)
    {
        _dataLayer = dataLayer;
        _resumeStorage = resumeStorage;
    }

    public async Task<CandidateProfileServiceModel?> GetAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var profile = await _dataLayer.GetAsync(candidateId, cancellationToken);
        return profile?.ToServiceModel();
    }

    public async Task<CandidateProfileServiceModel> UpsertAsync(Guid candidateId, UpsertCandidateProfileViewModel viewModel, CancellationToken cancellationToken = default)
    {
        var incoming = viewModel.ToEntity(candidateId);

        // The résumé is managed only through the upload/delete endpoints, never a profile save. Carry the
        // existing pointers onto the incoming entity so the upsert's SetValues doesn't null them out.
        var existing = await _dataLayer.GetAsync(candidateId, cancellationToken);
        if (existing is not null)
        {
            incoming.ResumeObjectName = existing.ResumeObjectName;
            incoming.ResumeFileName = existing.ResumeFileName;
            incoming.ResumeContentType = existing.ResumeContentType;
        }

        var saved = await _dataLayer.UpsertAsync(incoming, cancellationToken);
        return saved.ToServiceModel();
    }

    public async Task<CandidateProfileServiceModel> UploadResumeAsync(
        Guid candidateId,
        Stream content,
        string contentType,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var profile = await RequireProfileAsync(candidateId, cancellationToken);
        var previousObjectName = profile.ResumeObjectName;

        // Store the new blob first; only once it's committed do we repoint the profile at it.
        var objectName = await _resumeStorage.UploadAsync(candidateId, content, contentType, cancellationToken);
        profile.ResumeObjectName = objectName;
        profile.ResumeFileName = fileName;
        profile.ResumeContentType = contentType;
        profile.UpdatedOnUtc = DateTime.UtcNow;

        var saved = await _dataLayer.UpsertAsync(profile, cancellationToken);

        // The profile now points at the new blob; best-effort cleanup of the one it replaced.
        if (previousObjectName is not null && previousObjectName != objectName)
        {
            await _resumeStorage.DeleteAsync(previousObjectName, cancellationToken);
        }

        return saved.ToServiceModel();
    }

    public async Task<CandidateResumeFile?> GetResumeAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var profile = await _dataLayer.GetAsync(candidateId, cancellationToken);
        if (profile?.ResumeObjectName is null)
        {
            return null;
        }

        var download = await _resumeStorage.DownloadAsync(profile.ResumeObjectName, cancellationToken);
        if (download is null)
        {
            return null;
        }

        var fileName = profile.ResumeFileName ?? "resume";
        var contentType = profile.ResumeContentType ?? download.ContentType;
        return new CandidateResumeFile(download.Content, contentType, fileName);
    }

    public async Task<CandidateProfileServiceModel?> DeleteResumeAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var profile = await _dataLayer.GetAsync(candidateId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var objectName = profile.ResumeObjectName;
        profile.ResumeObjectName = null;
        profile.ResumeFileName = null;
        profile.ResumeContentType = null;
        profile.UpdatedOnUtc = DateTime.UtcNow;

        var saved = await _dataLayer.UpsertAsync(profile, cancellationToken);

        // Drop the blob after the pointer is cleared (a leftover blob is harmless; a dangling pointer isn't).
        if (objectName is not null)
        {
            await _resumeStorage.DeleteAsync(objectName, cancellationToken);
        }

        return saved.ToServiceModel();
    }

    private async Task<CandidateProfile> RequireProfileAsync(Guid candidateId, CancellationToken cancellationToken) =>
        await _dataLayer.GetAsync(candidateId, cancellationToken)
        ?? throw new DomainException(
            "candidate_profile.not_found",
            "Create your profile before uploading a résumé.",
            StatusCodes.Status404NotFound);
}
