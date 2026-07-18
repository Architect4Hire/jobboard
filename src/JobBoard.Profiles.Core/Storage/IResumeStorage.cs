namespace JobBoard.Profiles.Core.Storage;

/// <summary>
/// The résumé blob-store seam the candidate business layer writes through — Profiles-specific cross-cutting
/// mechanism, kept in this service's <c>.Core</c> so the Azure Blob SDK dependency stays out of Shared and
/// every other service. The contract lives here; <see cref="ResumeStorage"/> is the Azurite/Blob-backed
/// implementation (wired to the AppHost "blobs" resource via the host's Aspire client integration).
/// </summary>
public interface IResumeStorage
{
    /// <summary>
    /// Stores <paramref name="content"/> as the candidate's résumé and returns the blob object name (key)
    /// to persist on the profile. Overwrites any blob at the same key. The object name is namespaced by
    /// <paramref name="candidateId"/> so one candidate's uploads can never collide with another's.
    /// </summary>
    Task<string> UploadAsync(Guid candidateId, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the résumé blob for reading, or returns <c>null</c> when no blob exists at
    /// <paramref name="objectName"/> (e.g. it was deleted out from under a stale pointer).
    /// </summary>
    Task<ResumeDownload?> DownloadAsync(string objectName, CancellationToken cancellationToken = default);

    /// <summary>Deletes the résumé blob at <paramref name="objectName"/>; a no-op if it's already gone.</summary>
    Task DeleteAsync(string objectName, CancellationToken cancellationToken = default);
}
