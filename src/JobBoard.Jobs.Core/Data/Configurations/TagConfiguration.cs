using JobBoard.Jobs.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobBoard.Jobs.Core.Data.Configurations;

/// <summary>EF Core mapping for <see cref="Tag"/>: app-assigned key, unique <c>Slug</c>.</summary>
internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(50);
        builder.Property(t => t.Slug).IsRequired().HasMaxLength(50);

        builder.HasIndex(t => t.Slug).IsUnique();
    }
}
