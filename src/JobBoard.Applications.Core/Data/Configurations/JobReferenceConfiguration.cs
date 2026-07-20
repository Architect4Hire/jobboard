using JobBoard.Applications.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobBoard.Applications.Core.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="JobReference"/>: keyed by the owning job's id (app-assigned, mirrored
/// from Jobs), column length matching Jobs' own <c>Title</c> mapping.
/// </summary>
internal sealed class JobReferenceConfiguration : IEntityTypeConfiguration<JobReference>
{
    public void Configure(EntityTypeBuilder<JobReference> builder)
    {
        builder.ToTable("JobReferences");

        builder.HasKey(j => j.JobId);
        builder.Property(j => j.JobId).ValueGeneratedNever();

        builder.Property(j => j.Title).IsRequired().HasMaxLength(200);
        builder.Property(j => j.EmployerId).IsRequired();
    }
}
