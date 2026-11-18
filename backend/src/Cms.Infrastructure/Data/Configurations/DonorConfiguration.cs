using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class DonorConfiguration : IEntityTypeConfiguration<Donor>
{
    public void Configure(EntityTypeBuilder<Donor> builder)
    {
        builder.ToTable("Donors", "don");

        builder.HasKey(donor => donor.Id);

        builder.Property(donor => donor.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(donor => donor.DonorType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(donor => donor.ContactEmail)
            .HasMaxLength(320);

        builder.Property(donor => donor.ContactPhone)
            .HasMaxLength(20);

        builder.Property(donor => donor.IsAnonymous)
            .IsRequired();

        builder.Property(donor => donor.IsActive)
            .IsRequired();

        builder.HasIndex(donor => donor.Name, "IX_donors_name");
        builder.HasIndex(donor => donor.IsActive, "IX_donors_is_active");
    }
}
