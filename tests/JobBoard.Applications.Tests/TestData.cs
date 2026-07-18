using JobBoard.Applications.Core.Managers.Models.Domain;
using JobBoard.Applications.Core.Managers.Models.ViewModels;

namespace JobBoard.Applications.Tests;

/// <summary>Builders for the fixtures the Applications tests share, kept terse and override-friendly.</summary>
internal static class TestData
{
    public static Application Application(
        Guid? id = null,
        Guid? candidateId = null,
        Guid? jobId = null,
        ApplicationStatus status = ApplicationStatus.Submitted,
        string? resumeReference = "resume-ref")
    {
        var now = DateTime.UtcNow;
        return new Application
        {
            Id = id ?? Guid.NewGuid(),
            CandidateId = candidateId ?? Guid.NewGuid(),
            JobId = jobId ?? Guid.NewGuid(),
            Status = status,
            ResumeReference = resumeReference,
            SubmittedOnUtc = now,
            StatusChangedOnUtc = now,
        };
    }

    public static SubmitApplicationViewModel SubmitViewModel(
        Guid? candidateId = null,
        Guid? jobId = null,
        string? resumeReference = "resume-ref") => new()
    {
        CandidateId = candidateId ?? Guid.NewGuid(),
        JobId = jobId ?? Guid.NewGuid(),
        ResumeReference = resumeReference,
    };

    public static AdvanceApplicationStatusViewModel AdvanceViewModel(ApplicationStatus targetStatus) =>
        new() { TargetStatus = targetStatus };
}
