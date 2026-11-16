using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class RequestLinkConfiguration
    : IEntityTypeConfiguration<RequestLink>
{
    public void Configure(EntityTypeBuilder<RequestLink> builder)
    {
        builder.ToTable("RequestLinks");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.SourceRequestId)
            .IsRequired();

        builder.Property(l => l.TargetRequestId)
            .IsRequired();

        builder.Property(l => l.LinkType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.Remarks)
            .HasMaxLength(1000);

        builder.Property(l => l.LinkedByUserId)
            .HasMaxLength(450);

        builder.Property(l => l.LinkedAt)
            .IsRequired();

        builder.HasOne(l => l.SourceRequest)
            .WithMany()
            .HasForeignKey(l => l.SourceRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.TargetRequest)
            .WithMany()
            .HasForeignKey(l => l.TargetRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.SourceRequestId);
        builder.HasIndex(l => l.TargetRequestId);
    }
}
