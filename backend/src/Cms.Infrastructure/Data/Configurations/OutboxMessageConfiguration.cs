using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

/// <summary>
/// Maps <see cref="OutboxMessage"/> to the Dev 1-owned <c>common.outbox_messages</c>
/// table created by <c>00_common.sql</c>.
/// </summary>
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", "common");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.EntityId)
            .IsRequired();

        builder.Property(o => o.Payload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(o => o.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.RetryCount)
            .IsRequired();

        builder.Property(o => o.ErrorMessage)
            .HasColumnType("nvarchar(max)");

        builder.Property(o => o.CreatedAt)
            .IsRequired();

        builder.HasIndex(o => o.IdempotencyKey, "UX_outbox_idempotency_key")
            .IsUnique();

        // SQL Server filtered-index predicates support simple equality but not
        // OR/IN. The v4 contract's single OR index is therefore split into two
        // equivalent filtered indexes. Raw filter text is not rewritten by
        // NamingConventions, so it uses the snake_case column name.
        builder.HasIndex(
                o => new { o.Status, o.NextRetryAt },
                "IX_outbox_status_retry_pending")
            .HasFilter("[status] = 'Pending'");

        builder.HasIndex(
                o => new { o.Status, o.NextRetryAt },
                "IX_outbox_status_retry_failed")
            .HasFilter("[status] = 'Failed'");

        builder.HasIndex(o => o.CreatedAt, "IX_outbox_created_at");
    }
}
