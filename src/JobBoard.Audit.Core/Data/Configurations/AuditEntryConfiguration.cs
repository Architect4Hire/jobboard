using JobBoard.Audit.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobBoard.Audit.Core.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="AuditEntry"/>: app-assigned key (the source event id), the thread
/// columns, the event as a <c>jsonb</c> payload, and indexes on the two support-query axes
/// (<see cref="AuditEntry.CorrelationId"/> for a request's whole fan-out, <see cref="AuditEntry.SubjectId"/>
/// for one entity's life — SCRUB A6).
/// </summary>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.EventType).IsRequired().HasMaxLength(200);
        builder.Property(a => a.CorrelationId).IsRequired();
        builder.Property(a => a.CausationId).IsRequired();
        builder.Property(a => a.ActorId);
        builder.Property(a => a.SubjectId);
        builder.Property(a => a.OccurredOnUtc).IsRequired();

        // Heterogeneous event shapes: store the serialized event in a jsonb column so new event types
        // are audited without a migration.
        builder.Property(a => a.Payload).IsRequired().HasColumnType("jsonb");

        builder.HasIndex(a => a.CorrelationId);
        builder.HasIndex(a => a.SubjectId);
    }
}
