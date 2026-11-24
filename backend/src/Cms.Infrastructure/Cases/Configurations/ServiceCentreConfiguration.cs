using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Cases.Configurations;

public class ServiceCentreConfiguration : IEntityTypeConfiguration<ServiceCentre>
{
    public void Configure(EntityTypeBuilder<ServiceCentre> builder)
    {
        builder.ToTable("service_centres", "cases");
        builder.HasKey(s => s.Id).HasName("PK_service_centres");

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.CrisisId).HasColumnName("crisis_id");
        builder.Property(s => s.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(s => s.Address).HasColumnName("address").IsRequired().HasMaxLength(500);
        builder.Property(s => s.Lat).HasColumnName("lat").HasPrecision(18, 7);
        builder.Property(s => s.Lng).HasColumnName("lng").HasPrecision(18, 7);
        builder.Property(s => s.Services).HasColumnName("services").IsRequired().HasMaxLength(1000);
        builder.Property(s => s.OpeningHours).HasColumnName("opening_hours").HasMaxLength(500);
        builder.Property(s => s.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(s => s.PublicVisible).HasColumnName("public_visible")
            .HasColumnType("bit").IsRequired().HasDefaultValue(true);
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Crisis>().WithMany().HasForeignKey(s => s.CrisisId)
            .HasConstraintName("FK_service_centres_crises").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.IsActive).HasDatabaseName("IX_service_centres_is_active");
        builder.HasIndex(s => new { s.PublicVisible, s.IsActive })
            .HasDatabaseName("IX_service_centres_public")
            .HasFilter("[public_visible] = 1");
    }
}
