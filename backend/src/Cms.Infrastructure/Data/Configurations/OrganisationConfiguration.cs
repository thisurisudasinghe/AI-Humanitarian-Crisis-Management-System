using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable("Organisations", "common");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.OrganisationType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.RegistrationNumber)
            .HasMaxLength(100);

        builder.Property(o => o.ContactEmail)
            .HasMaxLength(500);

        builder.Property(o => o.ContactPhone)
            .HasMaxLength(20);

        builder.Property(o => o.Address)
            .HasMaxLength(500);

        builder.Property(o => o.IsActive)
            .IsRequired();

        builder.HasIndex(o => o.Name, "IX_organisations_name");
    }
}
