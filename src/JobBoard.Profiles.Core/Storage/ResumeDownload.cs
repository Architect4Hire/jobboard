namespace JobBoard.Profiles.Core.Storage;

/// <summary>
/// A résumé streamed back out of blob storage: the content <paramref name="Content"/> and the
/// <paramref name="ContentType"/> to replay so the browser handles the file correctly. The caller owns
/// disposing the stream (it's handed straight to a <c>FileStreamResult</c>).
/// </summary>
public sealed record ResumeDownload(Stream Content, string ContentType);
