using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class ServiceCentreConfiguration
    : IEntityTypeConfiguration<ServiceCentre>
{
    public void Configure(EntityTypeBuilder<ServiceCentre> builder)
    {
        builder.ToTable("ServiceCentres");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .HasMaxLength(2000);

        builder.Property(s => s.Address)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.ContactNumber)
            .HasMaxLength(20);

        builder.Property(s => s.Email)
            .HasMaxLength(256);

        builder.Property(s => s.District)
            .HasMaxLength(100);

        builder.Property(s => s.City)
            .HasMaxLength(100);

        builder.Property(s => s.Latitude)
            .HasPrecision(10, 7);

        builder.Property(s => s.Longitude)
            .HasPrecision(10, 7);

        builder.Property(s => s.IsActive)
            .IsRequired();
    }
}
