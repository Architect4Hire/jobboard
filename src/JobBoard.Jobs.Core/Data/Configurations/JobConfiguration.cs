using JobBoard.Jobs.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobBoard.Jobs.Core.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Job"/>: app-assigned key, the owned <see cref="SalaryBand"/> inlined
/// as <c>Salary_*</c> columns, <see cref="JobStatus"/> left as its default <c>int</c> mapping, and the
/// two many-to-many classifications with explicitly named join tables.
/// </summary>
internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();

        builder.Property(j => j.Title).IsRequired().HasMaxLength(200);
        builder.Property(j => j.Description).IsRequired();
        builder.Property(j => j.Location).IsRequired().HasMaxLength(200);

        // Stored as int (the enum's underlying value) — no conversion.
        builder.Property(j => j.Status).IsRequired();

        builder.Property(j => j.EmployerId).IsRequired();
        builder.Property(j => j.CreatedOnUtc).IsRequired();

        builder.OwnsOne(j => j.Salary, salary =>
        {
            salary.Property(s => s.Min).HasColumnName("Salary_Min").HasPrecision(18, 2);
            salary.Property(s => s.Max).HasColumnName("Salary_Max").HasPrecision(18, 2);
            salary.Property(s => s.Currency).HasColumnName("Salary_Currency").IsRequired().HasMaxLength(3);
        });
        builder.Navigation(j => j.Salary).IsRequired();

        builder.HasMany(j => j.Categories)
            .WithMany(c => c.Jobs)
            .UsingEntity(join => join.ToTable("JobCategories"));

        builder.HasMany(j => j.Tags)
            .WithMany(t => t.Jobs)
            .UsingEntity(join => join.ToTable("JobTags"));

        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.EmployerId);
    }
}
