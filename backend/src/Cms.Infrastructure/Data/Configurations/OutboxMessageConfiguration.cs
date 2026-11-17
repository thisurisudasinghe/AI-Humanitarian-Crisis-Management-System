using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class OutboxMessageConfiguration
    : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", "notif");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Type)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.Payload)
            .HasColumnName("PayloadJson")
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(o => o.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.EntityId)
            .IsRequired();
        builder.Property(o => o.ProcessingStartedAt);

        builder.Property(o => o.Attempts)
            .IsRequired();

        builder.Ignore(o => o.RetryCount);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(o => o.NextRetryAt)
            .HasColumnName("NextAttemptAt");

        builder.Ignore(o => o.NextAttemptAt);

        builder.Property(o => o.ErrorMessage)
            .HasColumnName("LastError")
            .HasMaxLength(2000);

        builder.Ignore(o => o.LastError);

        builder.HasIndex(o => o.IdempotencyKey)
            .IsUnique();

        builder.HasIndex(o => new { o.Status, o.NextRetryAt });

        builder.HasIndex(o => o.CreatedAt);
    }
}