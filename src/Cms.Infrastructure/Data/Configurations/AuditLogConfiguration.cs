
using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .HasMaxLength(100);

        builder.Property(a => a.Details)
            .HasMaxLength(2000);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UserId);
    }
}
