using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class DonationItemConfiguration : IEntityTypeConfiguration<DonationItem>
{
    public void Configure(EntityTypeBuilder<DonationItem> builder)
    {
        builder.ToTable("DonationItems", "don");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(item => item.Category)
            .HasMaxLength(80);

        builder.Property(item => item.Quantity)
            .IsRequired();

        builder.Property(item => item.Unit)
            .HasMaxLength(80);

        builder.Property(item => item.Condition)
            .HasColumnName("item_condition")
            .HasMaxLength(100);

        builder.Property(item => item.Notes)
            .HasMaxLength(500);

        builder.Property(item => item.Packaging)
            .HasMaxLength(200);

        builder.HasOne(item => item.Donation)
            .WithMany(donation => donation.Items)
            .HasForeignKey(item => item.DonationId)
            .HasConstraintName("FK_donation_items_donations")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => item.DonationId, "IX_donation_items_donation_id");
    }
}
