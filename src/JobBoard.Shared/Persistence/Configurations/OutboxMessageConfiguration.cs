using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobBoard.Shared.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="OutboxMessage"/>. Provider-agnostic on purpose (no <c>jsonb</c>): the
/// same configuration must apply under Postgres in a service and under SQLite in the Shared tests.
/// </summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Type).IsRequired().HasMaxLength(500);
        builder.Property(m => m.Destination).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.OccurredOnUtc).IsRequired();

        // The dispatcher polls unprocessed rows oldest-first.
        builder.HasIndex(m => new { m.ProcessedOnUtc, m.OccurredOnUtc });
    }
}
