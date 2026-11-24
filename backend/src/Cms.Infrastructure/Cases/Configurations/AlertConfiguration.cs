using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Cases.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts", "cases");
        builder.HasKey(a => a.Id).HasName("PK_alerts");

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.CrisisId).HasColumnName("crisis_id");
        builder.Property(a => a.Title).HasColumnName("title").IsRequired().HasMaxLength(200);
        builder.Property(a => a.Body).HasColumnName("body").IsRequired().HasMaxLength(2000);
        builder.Property(a => a.Status).HasColumnName("status")
            .HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(a => a.PublishedAt).HasColumnName("published_at");
        builder.Property(a => a.PublicVisible).HasColumnName("public_visible")
            .HasColumnType("bit").IsRequired().HasDefaultValue(false);
        builder.Property(a => a.PublicTitle).HasColumnName("public_title")
            .HasColumnType("nvarchar(200)").HasMaxLength(200);
        builder.Property(a => a.PublicSummary).HasColumnName("public_summary")
            .HasColumnType("nvarchar(1000)").HasMaxLength(1000);
        builder.Property(a => a.PublicType).HasColumnName("public_type")
            .HasColumnType("nvarchar(40)").HasMaxLength(40);
        builder.Property(a => a.Severity).HasColumnName("severity")
            .HasColumnType("nvarchar(30)").HasMaxLength(30);
        builder.Property(a => a.District).HasColumnName("district")
            .HasColumnType("nvarchar(120)").HasMaxLength(120);
        builder.Property(a => a.Latitude).HasColumnName("latitude").HasColumnType("float");
        builder.Property(a => a.Longitude).HasColumnName("longitude").HasColumnType("float");
        builder.Property(a => a.PublicExpiresAt).HasColumnName("public_expires_at")
            .HasColumnType("datetime2");
        builder.Property(a => a.PublishedByUserId).HasColumnName("published_by_user_id");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Crisis>().WithMany().HasForeignKey(a => a.CrisisId)
            .HasConstraintName("FK_alerts_crises").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.Status, a.PublishedAt })
            .HasDatabaseName("IX_alerts_status_published_at");
        builder.HasIndex(a => new { a.PublicVisible, a.Status, a.PublicExpiresAt })
            .HasDatabaseName("IX_alerts_public_map")
            .HasFilter("[public_visible] = 1");
    }
}
