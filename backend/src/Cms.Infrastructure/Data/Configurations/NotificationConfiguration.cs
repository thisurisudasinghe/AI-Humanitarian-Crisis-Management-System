
using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(n => n.Channel)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(n => n.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_notifications_users");

        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        builder.HasIndex(n => n.Status);
    }
}
