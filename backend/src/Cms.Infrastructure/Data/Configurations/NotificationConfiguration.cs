using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

/// <summary>
/// Maps <see cref="Notification"/> to the Dev 1-owned
/// <c>common.notifications</c> table created by <c>00_common.sql</c>.
/// </summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", "common");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Channel)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(n => n.TemplateKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(n => n.Subject)
            .HasMaxLength(500);

        builder.Property(n => n.Body)
            .HasColumnType("nvarchar(max)");

        builder.Property(n => n.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(n => n.CreatedAt)
            .IsRequired();

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_notifications_users");

        builder.HasIndex(
                n => new { n.RecipientUserId, n.CreatedAt },
                "IX_notifications_recipient");

        builder.HasIndex(n => n.Status, "IX_notifications_status")
            .HasFilter("[status] = 'Pending'");

        // FR-COM-07: one row per (outbox event, channel), so a retried event
        // updates the existing delivery instead of creating a duplicate.
        builder.HasIndex(
                n => new { n.OutboxMessageId, n.Channel },
                "UX_notifications_outbox_channel")
            .IsUnique()
            .HasFilter("[outbox_message_id] IS NOT NULL");
    }
}
