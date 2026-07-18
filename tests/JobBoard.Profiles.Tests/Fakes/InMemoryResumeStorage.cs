using JobBoard.Profiles.Core.Storage;

namespace JobBoard.Profiles.Tests.Fakes;

/// <summary>
/// Dictionary-backed <see cref="IResumeStorage"/> for tests — stores résumé bytes in memory so the
/// business layer and the endpoint pipeline can exercise the full upload → download → delete flow without
/// Azurite. Registered as a singleton in the endpoint factory so uploads survive across requests.
/// </summary>
public sealed class InMemoryResumeStorage : IResumeStorage
{
    private readonly Dictionary<string, (byte[] Bytes, string ContentType)> _blobs = new();

    public int UploadCount { get; private set; }

    public List<string> Deleted { get; } = [];

    public int Count => _blobs.Count;

    public async Task<string> UploadAsync(Guid candidateId, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var objectName = $"{candidateId}/{Guid.NewGuid():n}";
        _blobs[objectName] = (buffer.ToArray(), contentType);
        UploadCount++;
        return objectName;
    }

    public Task<ResumeDownload?> DownloadAsync(string objectName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_blobs.TryGetValue(objectName, out var blob)
            ? new ResumeDownload(new MemoryStream(blob.Bytes), blob.ContentType)
            : null);

    public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        _blobs.Remove(objectName);
        Deleted.Add(objectName);
        return Task.CompletedTask;
    }
}
