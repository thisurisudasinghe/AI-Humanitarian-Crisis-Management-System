using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class CrisisConfiguration : IEntityTypeConfiguration<Crisis>
{
    public void Configure(EntityTypeBuilder<Crisis> builder)
    {
        builder.ToTable("Crises");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .HasMaxLength(2000);

        builder.Property(c => c.CrisisType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Severity)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.StartDate)
            .IsRequired();

        builder.Property(c => c.EndDate);

        builder.Property(c => c.IsActive)
            .IsRequired();

        builder.HasMany(c => c.Areas)
            .WithOne()
            .HasForeignKey(a => a.CrisisId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
