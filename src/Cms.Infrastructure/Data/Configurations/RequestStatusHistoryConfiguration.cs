using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class RequestStatusHistoryConfiguration
    : IEntityTypeConfiguration<RequestStatusHistory>
{
    public void Configure(EntityTypeBuilder<RequestStatusHistory> builder)
    {
        builder.ToTable("RequestStatusHistory");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.RequestId)
            .IsRequired();

        builder.Property(h => h.PreviousStatus)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(h => h.NewStatus)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(h => h.ChangedByUserId)
            .HasMaxLength(450);

        builder.Property(h => h.Remarks)
            .HasMaxLength(1000);

        builder.Property(h => h.ChangedAt)
            .IsRequired();

        builder.HasOne(h => h.Request)
            .WithMany(r => r.StatusHistory)
            .HasForeignKey(h => h.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => h.RequestId);
    }
}
