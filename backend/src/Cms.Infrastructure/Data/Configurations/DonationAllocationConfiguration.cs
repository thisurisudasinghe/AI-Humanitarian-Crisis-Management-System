using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class DonationAllocationConfiguration
    : IEntityTypeConfiguration<DonationAllocation>
{
    public void Configure(EntityTypeBuilder<DonationAllocation> builder)
    {
        builder.ToTable("DonationAllocations", "don");

        builder.HasKey(allocation => allocation.Id);

        builder.Property(allocation => allocation.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(allocation => allocation.Note)
            .HasMaxLength(500);

        builder.Property(allocation => allocation.SuspiciousReason)
            .HasMaxLength(500);

        builder.HasOne(allocation => allocation.Donation)
            .WithMany(donation => donation.Allocations)
            .HasForeignKey(allocation => allocation.DonationId)
            .HasConstraintName("FK_donation_allocations_donations")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(allocation => allocation.Campaign)
            .WithMany(campaign => campaign.Allocations)
            .HasForeignKey(allocation => allocation.CampaignId)
            .HasConstraintName("FK_donation_allocations_campaigns")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(
            allocation => allocation.DonationId,
            "IX_donation_allocations_donation_id");

        builder.HasIndex(
            allocation => allocation.CampaignId,
            "IX_donation_allocations_campaign_id");
    }
}
