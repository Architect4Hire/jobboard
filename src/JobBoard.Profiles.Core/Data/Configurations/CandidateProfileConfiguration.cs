using JobBoard.Profiles.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobBoard.Profiles.Core.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="CandidateProfile"/>: app-assigned key (the candidate's account id), and
/// <see cref="CandidateProfile.Skills"/> stored as a single <b>newline-delimited</b> text column via a
/// value converter + comparer. Provider-agnostic (plain <c>text</c>, no jsonb/array) so it round-trips
/// identically under Postgres and the SQLite tests.
/// </summary>
internal sealed class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    private const char Delimiter = '\n';

    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.ToTable("CandidateProfiles");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Headline).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Summary).IsRequired().HasMaxLength(4000);

        // Contact & location (all optional).
        builder.Property(p => p.FullName).HasMaxLength(200);
        builder.Property(p => p.Location).HasMaxLength(200);
        builder.Property(p => p.Phone).HasMaxLength(50);

        // Professional links (all optional; validated as absolute http(s) URLs at the edge).
        builder.Property(p => p.LinkedInUrl).HasMaxLength(2048);
        builder.Property(p => p.GitHubUrl).HasMaxLength(2048);
        builder.Property(p => p.PortfolioUrl).HasMaxLength(2048);

        // Experience & availability. Availability persists by NAME (not its ordinal) so reordering the
        // enum can't silently repoint stored rows; provider-agnostic text round-trips under both providers.
        builder.Property(p => p.DesiredRole).HasMaxLength(200);
        builder.Property(p => p.Availability).HasConversion<string>().HasMaxLength(20);

        // Résumé file metadata (the bytes live in blob storage; only the pointers live here).
        builder.Property(p => p.ResumeObjectName).HasMaxLength(512);
        builder.Property(p => p.ResumeFileName).HasMaxLength(260);
        builder.Property(p => p.ResumeContentType).HasMaxLength(100);

        builder.Property(p => p.UpdatedOnUtc).IsRequired();

        var skillsConverter = new ValueConverter<List<string>, string>(
            skills => string.Join(Delimiter, skills),
            value => value.Length == 0
                ? new List<string>()
                : value.Split(Delimiter, StringSplitOptions.RemoveEmptyEntries).ToList());

        var skillsComparer = new ValueComparer<List<string>>(
            (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
            skills => skills.Aggregate(0, (hash, skill) => HashCode.Combine(hash, skill.GetHashCode())),
            skills => skills.ToList());

        builder.Property(p => p.Skills)
            .HasConversion(skillsConverter, skillsComparer)
            .HasColumnName("Skills")
            .IsRequired();
    }
}
