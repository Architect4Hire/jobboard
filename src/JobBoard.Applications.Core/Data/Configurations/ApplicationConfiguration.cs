using JobBoard.Applications.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobBoard.Applications.Core.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Application"/>: app-assigned key, <see cref="ApplicationStatus"/> left as
/// its default <c>int</c> mapping, and a <b>unique</b> index on <c>(CandidateId, JobId)</c> — a candidate
/// applies to a given job at most once; the narrow race where two concurrent submits slip past the
/// read-side check trips this index, and the data layer maps that to a retryable 409. Lookup indexes back
/// the two hot queries: list-by-candidate and the consumer's close-by-job.
/// </summary>
internal sealed class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("Applications");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.CandidateId).IsRequired();
        builder.Property(a => a.JobId).IsRequired();

        // Stored as int (the enum's underlying value) — no conversion.
        builder.Property(a => a.Status).IsRequired();

        builder.Property(a => a.ResumeReference).HasMaxLength(2048);

        builder.Property(a => a.SubmittedOnUtc).IsRequired();
        builder.Property(a => a.StatusChangedOnUtc).IsRequired();

        // One application per candidate per job — the guard the submit conflict maps against.
        builder.HasIndex(a => new { a.CandidateId, a.JobId }).IsUnique();

        // Backs GET /applications?candidateId=… and the JobClosed consumer's close-by-job query.
        builder.HasIndex(a => a.CandidateId);
        builder.HasIndex(a => a.JobId);
    }
}
