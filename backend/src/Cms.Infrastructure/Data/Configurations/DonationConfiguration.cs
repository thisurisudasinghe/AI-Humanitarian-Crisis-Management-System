using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class DonationConfiguration : IEntityTypeConfiguration<Donation>
{
    public void Configure(EntityTypeBuilder<Donation> builder)
    {
        builder.ToTable("Donations", "don");

        builder.HasKey(donation => donation.Id);

        builder.Property(donation => donation.Reference)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(donation => donation.DonationType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(donation => donation.Amount)
            .HasPrecision(18, 2);

        builder.Property(donation => donation.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(donation => donation.ItemDescription)
            .HasMaxLength(500);

        builder.Property(donation => donation.EstimatedValue)
            .HasPrecision(18, 2);

        builder.Property(donation => donation.Category)
            .HasMaxLength(80);

        builder.Property(donation => donation.Unit)
            .HasMaxLength(80);

        builder.Property(donation => donation.LocationText)
            .HasMaxLength(300);

        // Coordinates are CLR doubles; SQL Server decimal columns cannot be read
        // into a double by the provider. Use the approximate numeric type that
        // matches the entity and DTOs, while validation still bounds the values.
        builder.Property(donation => donation.Latitude)
            .HasColumnType("float");

        builder.Property(donation => donation.Longitude)
            .HasColumnType("float");

        builder.Property(donation => donation.DeliveryMethod)
            .HasMaxLength(80);

        builder.Property(donation => donation.IdempotencyKey)
            .HasMaxLength(100);

        builder.Property(donation => donation.ReceivedAt)
            .IsRequired();

        builder.Property(donation => donation.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(donation => donation.SuspiciousReason)
            .HasMaxLength(500);

        builder.Property(donation => donation.Notes)
            .HasMaxLength(1000);

        builder.HasOne(donation => donation.Donor)
            .WithMany(donor => donor.Donations)
            .HasForeignKey(donation => donation.DonorId)
            .HasConstraintName("FK_donations_donors")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(donation => donation.Campaign)
            .WithMany()
            .HasForeignKey(donation => donation.CampaignId)
            .HasConstraintName("FK_donations_campaigns_portal")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(donation => donation.Reference, "UX_donations_reference")
            .IsUnique();

        builder.HasIndex(donation => donation.IdempotencyKey, "UX_donations_idempotency_key")
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL");

        builder.HasIndex(donation => donation.CampaignId, "IX_donations_campaign_id");
        builder.HasIndex(donation => donation.DonorId, "IX_donations_donor_id");
        builder.HasIndex(donation => donation.ReceivedAt, "IX_donations_received_at");
        builder.HasIndex(donation => donation.Status, "IX_donations_status");
        builder.HasIndex(donation => donation.IsSuspicious, "IX_donations_is_suspicious");
    }
}
