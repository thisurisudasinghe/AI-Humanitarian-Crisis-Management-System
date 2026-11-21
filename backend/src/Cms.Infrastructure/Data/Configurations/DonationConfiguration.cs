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

        builder.HasIndex(donation => donation.Reference, "UX_donations_reference")
            .IsUnique();

        builder.HasIndex(donation => donation.DonorId, "IX_donations_donor_id");
        builder.HasIndex(donation => donation.ReceivedAt, "IX_donations_received_at");
        builder.HasIndex(donation => donation.Status, "IX_donations_status");
        builder.HasIndex(donation => donation.IsSuspicious, "IX_donations_is_suspicious");
    }
}
