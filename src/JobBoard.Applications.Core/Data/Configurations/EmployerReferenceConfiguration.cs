using JobBoard.Applications.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobBoard.Applications.Core.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="EmployerReference"/>: keyed by the owning employer's id (app-assigned,
/// mirrored from Profiles), column length matching Profiles' own <c>CompanyName</c> mapping.
/// </summary>
internal sealed class EmployerReferenceConfiguration : IEntityTypeConfiguration<EmployerReference>
{
    public void Configure(EntityTypeBuilder<EmployerReference> builder)
    {
        builder.ToTable("EmployerReferences");

        builder.HasKey(e => e.EmployerId);
        builder.Property(e => e.EmployerId).ValueGeneratedNever();

        builder.Property(e => e.CompanyName).IsRequired().HasMaxLength(200);
    }
}
