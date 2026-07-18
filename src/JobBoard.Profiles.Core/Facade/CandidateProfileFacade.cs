using FluentValidation;
using JobBoard.Profiles.Core.Business;
using JobBoard.Profiles.Core.Managers.Models.ServiceModels;
using JobBoard.Profiles.Core.Managers.Models.ViewModels;
using JobBoard.Profiles.Core.Storage;
using JobBoard.Shared.Errors;
using Microsoft.AspNetCore.Http;

namespace JobBoard.Profiles.Core.Facade;

/// <inheritdoc cref="ICandidateProfileFacade"/>
/// <remarks>No caching: profiles are read straight through and written on upsert; the facade owns the
/// validation seam — the upsert view model via FluentValidation, and the uploaded résumé's size/type
/// inline — then delegates.</remarks>
public sealed class CandidateProfileFacade : ICandidateProfileFacade
{
    /// <summary>Cap on résumé size — generous for a document, small enough to reject a mis-picked file.</summary>
    public const long MaxResumeBytes = 5 * 1024 * 1024;

    // Accepted résumé formats, matched on content type OR extension (browsers vary on the type they send).
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx",
    };

    private readonly ICandidateProfileBusiness _business;
    private readonly IValidator<UpsertCandidateProfileViewModel> _upsertValidator;

    public CandidateProfileFacade(
        ICandidateProfileBusiness business,
        IValidator<UpsertCandidateProfileViewModel> upsertValidator)
    {
        _business = business;
        _upsertValidator = upsertValidator;
    }

    public Task<CandidateProfileServiceModel?> GetAsync(Guid candidateId, CancellationToken cancellationToken = default) =>
        _business.GetAsync(candidateId, cancellationToken);

    public async Task<CandidateProfileServiceModel> UpsertAsync(Guid candidateId, UpsertCandidateProfileViewModel viewModel, CancellationToken cancellationToken = default)
    {
        // The global exception handler maps the thrown ValidationException to a 400 with field detail.
        await _upsertValidator.ValidateAndThrowAsync(viewModel, cancellationToken);
        return await _business.UpsertAsync(candidateId, viewModel, cancellationToken);
    }

    public Task<CandidateProfileServiceModel> UploadResumeAsync(
        Guid candidateId,
        Stream content,
        long length,
        string contentType,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ValidateResume(length, contentType, fileName);
        return _business.UploadResumeAsync(candidateId, content, contentType, fileName, cancellationToken);
    }

    public Task<CandidateResumeFile?> GetResumeAsync(Guid candidateId, CancellationToken cancellationToken = default) =>
        _business.GetResumeAsync(candidateId, cancellationToken);

    public Task<CandidateProfileServiceModel?> DeleteResumeAsync(Guid candidateId, CancellationToken cancellationToken = default) =>
        _business.DeleteResumeAsync(candidateId, cancellationToken);

    // Edge validation of the upload itself (the analogue of the FluentValidator for the JSON body).
    private static void ValidateResume(long length, string contentType, string fileName)
    {
        if (length <= 0)
        {
            throw Invalid("The résumé file is empty.");
        }

        if (length > MaxResumeBytes)
        {
            throw Invalid($"The résumé must be {MaxResumeBytes / (1024 * 1024)} MB or smaller.");
        }

        var extension = Path.GetExtension(fileName);
        var typeAllowed = !string.IsNullOrWhiteSpace(contentType) && AllowedContentTypes.Contains(contentType);
        var extensionAllowed = !string.IsNullOrWhiteSpace(extension) && AllowedExtensions.Contains(extension);
        if (!typeAllowed && !extensionAllowed)
        {
            throw Invalid("The résumé must be a PDF or Word document (.pdf, .doc, .docx).");
        }
    }

    private static DomainException Invalid(string message) =>
        new("candidate_profile.resume_invalid", message, StatusCodes.Status400BadRequest);
}
