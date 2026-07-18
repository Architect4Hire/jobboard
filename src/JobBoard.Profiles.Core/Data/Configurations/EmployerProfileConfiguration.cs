using JobBoard.Profiles.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobBoard.Profiles.Core.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="EmployerProfile"/>: app-assigned key (the employer's account id) and
/// column lengths mirroring the validator.
/// </summary>
internal sealed class EmployerProfileConfiguration : IEntityTypeConfiguration<EmployerProfile>
{
    public void Configure(EntityTypeBuilder<EmployerProfile> builder)
    {
        builder.ToTable("EmployerProfiles");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.CompanyName).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Website).HasMaxLength(2048);
        builder.Property(p => p.Description).IsRequired().HasMaxLength(4000);
        builder.Property(p => p.UpdatedOnUtc).IsRequired();
    }
}
