using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("Alerts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.CrisisId)
            .IsRequired();

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Message)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(a => a.AlertType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Severity)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.IsActive)
            .IsRequired();

        builder.HasOne(a => a.Crisis)
            .WithMany()
            .HasForeignKey(a => a.CrisisId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.CrisisId);
    }
}
