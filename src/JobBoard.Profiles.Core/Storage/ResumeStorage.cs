using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace JobBoard.Profiles.Core.Storage;

/// <summary>
/// <see cref="IResumeStorage"/> over Azure Blob Storage — the Azurite emulator locally (a container the
/// AppHost runs), a real storage account in the cloud, same code. The <see cref="BlobServiceClient"/> is
/// supplied by the host's Aspire client integration keyed to the "blobs" resource; this type never sees a
/// connection string. All résumés live in one private container; access is only ever the owning
/// candidate's, brokered through the service's authenticated download endpoint (blobs stay private).
/// </summary>
public sealed class ResumeStorage : IResumeStorage
{
    private const string ContainerName = "resumes";

    private readonly BlobServiceClient _blobServiceClient;

    public ResumeStorage(BlobServiceClient blobServiceClient) => _blobServiceClient = blobServiceClient;

    public async Task<string> UploadAsync(Guid candidateId, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        // Namespace by candidate id and give each upload a fresh key, so a replace never has to delete
        // first and one candidate's blobs can't collide with another's.
        var objectName = $"{candidateId}/{Guid.NewGuid():n}";
        var blob = container.GetBlobClient(objectName);
        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);

        return objectName;
    }

    public async Task<ResumeDownload?> DownloadAsync(string objectName, CancellationToken cancellationToken = default)
    {
        var blob = _blobServiceClient.GetBlobContainerClient(ContainerName).GetBlobClient(objectName);
        try
        {
            var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            var contentType = response.Value.Details.ContentType ?? "application/octet-stream";
            return new ResumeDownload(response.Value.Content, contentType);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // The pointer outlived the blob (deleted out from under it) — treat as "no résumé".
            return null;
        }
    }

    public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        var blob = _blobServiceClient.GetBlobContainerClient(ContainerName).GetBlobClient(objectName);
        return blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
