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
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Crisis>().WithMany().HasForeignKey(a => a.CrisisId)
            .HasConstraintName("FK_alerts_crises").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.Status, a.PublishedAt })
            .HasDatabaseName("IX_alerts_status_published_at");
    }
}
