using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Cases.Configurations;

public class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("areas", "cases");
        builder.HasKey(a => a.Id).HasName("PK_areas");

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.CrisisId).HasColumnName("crisis_id").IsRequired();
        builder.Property(a => a.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(a => a.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(a => a.Lat).HasColumnName("lat").HasPrecision(18, 7);
        builder.Property(a => a.Lng).HasColumnName("lng").HasPrecision(18, 7);
        builder.Property(a => a.RadiusKm).HasColumnName("radius_km").HasPrecision(10, 2);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Crisis>().WithMany().HasForeignKey(a => a.CrisisId)
            .HasConstraintName("FK_areas_crises").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.CrisisId).HasDatabaseName("IX_areas_crisis_id");
    }
}
