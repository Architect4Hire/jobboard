using JobBoard.Notifications.Core.Managers.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobBoard.Notifications.Core.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="NotificationLog"/>: app-assigned key, string lengths, and an index on
/// <see cref="NotificationLog.RecipientId"/> for "my notifications" style reads.
/// </summary>
internal sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("NotificationLogs");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.RecipientId).IsRequired();
        builder.Property(n => n.Kind).IsRequired().HasMaxLength(100);
        builder.Property(n => n.Subject).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Body).IsRequired().HasMaxLength(2000);
        builder.Property(n => n.CreatedOnUtc).IsRequired();

        builder.HasIndex(n => n.RecipientId);
    }
}
