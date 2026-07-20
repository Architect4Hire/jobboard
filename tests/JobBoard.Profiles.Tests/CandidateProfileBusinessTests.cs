using System.Text;
using JobBoard.Profiles.Core.Business;
using JobBoard.Profiles.Tests.Fakes;
using JobBoard.Shared.Errors;
using JobBoard.Shared.Requests;
using Xunit;

namespace JobBoard.Profiles.Tests;

public sealed class CandidateProfileBusinessTests
{
    // The request thread the gateway projected; the business stamps it onto every ProfileUpdated it builds.
    private static readonly Guid CorrelationId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly IRequestContext RequestContext = BuildContext();

    private static AmbientRequestContext BuildContext()
    {
        var context = new AmbientRequestContext();
        context.Populate(CorrelationId, ActorId, "candidate");
        return context;
    }

    private static CandidateProfileBusiness Create(FakeCandidateProfileDataLayer dataLayer, InMemoryResumeStorage? storage = null) =>
        new(dataLayer, storage ?? new InMemoryResumeStorage(), RequestContext);

    // Asserts the ProfileUpdated built at a candidate write site: fresh id, the profile as subject, the
    // "Candidate" discriminator, and the request thread (actor is the authenticated caller — RootThread).
    private static void AssertCandidateEvent(FakeCandidateProfileDataLayer dataLayer, Guid candidateId)
    {
        var updated = dataLayer.UpdatedEvent;
        Assert.NotNull(updated);
        Assert.NotEqual(Guid.Empty, updated!.Id);
        Assert.Equal(candidateId, updated.ProfileId);
        Assert.Equal("Candidate", updated.ProfileType);
        Assert.Equal(CorrelationId, updated.CorrelationId);
        Assert.Equal(CorrelationId, updated.CausationId);
        Assert.Equal(ActorId, updated.ActorId);
    }

    [Fact]
    public async Task UpsertAsync_TranslatesViewModel_WithRouteOwnerId_AndMapsResult()
    {
        var dataLayer = new FakeCandidateProfileDataLayer();
        var business = Create(dataLayer);
        var candidateId = Guid.NewGuid();

        var result = await business.UpsertAsync(
            candidateId,
            TestData.CandidateViewModel(headline: "Staff Engineer", skills: ["c#", "azure"]));

        // The owner id came from the route, not the body; fields translated onto the entity.
        Assert.NotNull(dataLayer.Upserted);
        Assert.Equal(candidateId, dataLayer.Upserted!.Id);
        Assert.Equal("Staff Engineer", dataLayer.Upserted.Headline);
        Assert.Equal(["c#", "azure"], dataLayer.Upserted.Skills);
        Assert.Equal("Sam Example", dataLayer.Upserted.FullName);
        Assert.Equal(10, dataLayer.Upserted.YearsOfExperience);

        // The response mirrors the persisted entity.
        Assert.Equal(candidateId, result.CandidateId);
        Assert.Equal("Staff Engineer", result.Headline);
        Assert.Equal(["c#", "azure"], result.Skills);

        // The upsert emits ProfileUpdated (ids + type + timestamp only — no résumé PII).
        AssertCandidateEvent(dataLayer, candidateId);
    }

    [Fact]
    public async Task UpsertAsync_PreservesExistingResume_AcrossAProfileSave()
    {
        var candidateId = Guid.NewGuid();
        // A profile with a résumé already on file — a subsequent profile save must not wipe it.
        var dataLayer = new FakeCandidateProfileDataLayer
        {
            GetResult = TestData.CandidateProfile(id: candidateId, resumeObjectName: "blob-1", resumeFileName: "cv.pdf"),
        };
        var business = Create(dataLayer);

        var result = await business.UpsertAsync(candidateId, TestData.CandidateViewModel(headline: "Updated"));

        Assert.Equal("blob-1", dataLayer.Upserted!.ResumeObjectName);
        Assert.Equal("cv.pdf", dataLayer.Upserted.ResumeFileName);
        // The service model exposes the download path (not the blob key) once a résumé exists.
        Assert.Equal($"/profiles/candidates/{candidateId}/resume", result.ResumeUrl);
        Assert.Equal("cv.pdf", result.ResumeFileName);
    }

    [Fact]
    public async Task GetAsync_MapsEntity_OrReturnsNull()
    {
        var candidateId = Guid.NewGuid();
        var dataLayer = new FakeCandidateProfileDataLayer { GetResult = TestData.CandidateProfile(id: candidateId, headline: "Mapped") };
        var business = Create(dataLayer);

        var found = await business.GetAsync(candidateId);
        Assert.Equal("Mapped", found!.Headline);
        Assert.Equal(candidateId, found.CandidateId);

        var missingDataLayer = new FakeCandidateProfileDataLayer { GetResult = null };
        Assert.Null(await Create(missingDataLayer).GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UploadResumeAsync_NoProfile_Throws404()
    {
        var business = Create(new FakeCandidateProfileDataLayer { GetResult = null });

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => business.UploadResumeAsync(Guid.NewGuid(), new MemoryStream([1, 2, 3]), "application/pdf", "cv.pdf"));

        Assert.Equal("candidate_profile.not_found", ex.Code);
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task UploadResumeAsync_Stores_AndPointsProfileAtBlob()
    {
        var candidateId = Guid.NewGuid();
        var dataLayer = new FakeCandidateProfileDataLayer { GetResult = TestData.CandidateProfile(id: candidateId) };
        var storage = new InMemoryResumeStorage();
        var business = Create(dataLayer, storage);

        var result = await business.UploadResumeAsync(
            candidateId, new MemoryStream(Encoding.UTF8.GetBytes("pdf-bytes")), "application/pdf", "resume.pdf");

        Assert.Equal(1, storage.UploadCount);
        Assert.NotNull(dataLayer.Upserted!.ResumeObjectName);
        Assert.Equal("resume.pdf", dataLayer.Upserted.ResumeFileName);
        Assert.Equal("application/pdf", dataLayer.Upserted.ResumeContentType);
        Assert.Equal($"/profiles/candidates/{candidateId}/resume", result.ResumeUrl);

        // A résumé upload is a profile change — it too emits ProfileUpdated.
        AssertCandidateEvent(dataLayer, candidateId);
    }

    [Fact]
    public async Task UploadResumeAsync_Replacing_DeletesThePreviousBlob()
    {
        var candidateId = Guid.NewGuid();
        var dataLayer = new FakeCandidateProfileDataLayer
        {
            GetResult = TestData.CandidateProfile(id: candidateId, resumeObjectName: "old-blob", resumeFileName: "old.pdf"),
        };
        var storage = new InMemoryResumeStorage();
        var business = Create(dataLayer, storage);

        await business.UploadResumeAsync(candidateId, new MemoryStream([9]), "application/pdf", "new.pdf");

        Assert.Contains("old-blob", storage.Deleted);
    }

    [Fact]
    public async Task GetResumeAsync_ReturnsNull_WhenNoResume()
    {
        var dataLayer = new FakeCandidateProfileDataLayer { GetResult = TestData.CandidateProfile(resumeObjectName: null) };
        Assert.Null(await Create(dataLayer).GetResumeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteResumeAsync_ClearsPointers_AndRemovesBlob()
    {
        var candidateId = Guid.NewGuid();
        var dataLayer = new FakeCandidateProfileDataLayer
        {
            GetResult = TestData.CandidateProfile(id: candidateId, resumeObjectName: "blob-x", resumeFileName: "cv.pdf"),
        };
        var storage = new InMemoryResumeStorage();
        var business = Create(dataLayer, storage);

        var result = await business.DeleteResumeAsync(candidateId);

        Assert.NotNull(result);
        Assert.Null(dataLayer.Upserted!.ResumeObjectName);
        Assert.Null(result!.ResumeUrl);
        Assert.Contains("blob-x", storage.Deleted);

        // Clearing the résumé is a profile change — it too emits ProfileUpdated.
        AssertCandidateEvent(dataLayer, candidateId);
    }

    [Fact]
    public async Task DeleteResumeAsync_NoProfile_ReturnsNull()
    {
        var business = Create(new FakeCandidateProfileDataLayer { GetResult = null });
        Assert.Null(await business.DeleteResumeAsync(Guid.NewGuid()));
    }
}
