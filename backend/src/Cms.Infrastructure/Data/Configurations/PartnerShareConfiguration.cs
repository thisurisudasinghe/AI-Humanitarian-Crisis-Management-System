using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class PartnerShareConfiguration
    : IEntityTypeConfiguration<PartnerShare>
{
    public void Configure(EntityTypeBuilder<PartnerShare> builder)
    {
        builder.ToTable("PartnerShares");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.RequestId)
            .IsRequired();

        builder.Property(p => p.PartnerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.PartnerOrganization)
            .HasMaxLength(200);

        builder.Property(p => p.SharedData)
            .HasMaxLength(4000);

        builder.Property(p => p.SharedAt)
            .IsRequired();

        builder.Property(p => p.SharedByUserId)
            .HasMaxLength(450);

        builder.Property(p => p.Remarks)
            .HasMaxLength(1000);

        builder.HasOne(p => p.Request)
            .WithMany()
            .HasForeignKey(p => p.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.RequestId);
    }
}
