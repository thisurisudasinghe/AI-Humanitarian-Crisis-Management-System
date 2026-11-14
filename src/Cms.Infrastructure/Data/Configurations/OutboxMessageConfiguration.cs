
using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Type)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.Payload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(o => o.ErrorMessage)
            .HasMaxLength(2000);

        builder.HasIndex(
                o => new { o.Status, o.NextRetryAt },
                "IX_OutboxMessages_Status_NextRetryAt_Pending")
            .HasFilter("[Status] = 'Pending'");

        builder.HasIndex(
                o => new { o.Status, o.NextRetryAt },
                "IX_OutboxMessages_Status_NextRetryAt_Failed")
            .HasFilter("[Status] = 'Failed'");

        builder.HasIndex(o => o.CreatedAt);
    }
}
