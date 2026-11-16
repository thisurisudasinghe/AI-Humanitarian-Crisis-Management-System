using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable("Households");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.AreaId)
            .IsRequired();

        builder.Property(h => h.HouseholdName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(h => h.HeadOfHouseholdName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(h => h.ContactNumber)
            .HasMaxLength(20);

        builder.Property(h => h.Address)
            .HasMaxLength(500);

        builder.Property(h => h.HouseholdSize)
            .IsRequired();

        builder.Property(h => h.VulnerabilityLevel)
            .HasMaxLength(50);

        builder.Property(h => h.IsVerified)
            .IsRequired();

        builder.HasOne(h => h.Area)
            .WithMany()
            .HasForeignKey(h => h.AreaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
