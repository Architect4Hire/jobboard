namespace JobBoard.Profiles.Core.Storage;

/// <summary>
/// A résumé ready to stream to the client: the blob <paramref name="Content"/>, its
/// <paramref name="ContentType"/>, and the original <paramref name="FileName"/> (from the profile row, for
/// the download's <c>Content-Disposition</c>). Returned by the business/facade download path; the
/// controller hands <see cref="Content"/> straight to a <c>FileStreamResult</c>, which disposes it.
/// </summary>
public sealed record CandidateResumeFile(Stream Content, string ContentType, string FileName);
