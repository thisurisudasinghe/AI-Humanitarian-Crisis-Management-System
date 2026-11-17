using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents", "common");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .HasMaxLength(100);

        builder.Property(a => a.Details)
            .HasColumnType("nvarchar(max)");

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.HasIndex(a => a.CreatedAt, "IX_audit_events_created_at");
        builder.HasIndex(a => a.ActorUserId, "IX_audit_events_actor_user_id");
        builder.HasIndex(a => new { a.EntityType, a.EntityId }, "IX_audit_events_entity");
    }
}
