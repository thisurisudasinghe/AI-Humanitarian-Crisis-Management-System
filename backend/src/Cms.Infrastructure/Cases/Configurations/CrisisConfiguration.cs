using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Cases.Configurations;

public class CrisisConfiguration : IEntityTypeConfiguration<Crisis>
{
    public void Configure(EntityTypeBuilder<Crisis> builder)
    {
        builder.ToTable("crises", "cases");
        builder.HasKey(c => c.Id).HasName("PK_crises");

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(c => c.Type).HasColumnName("type").IsRequired().HasMaxLength(30);
        builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(c => c.Status).HasColumnName("status")
            .HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(c => c.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(c => c.EndedAt).HasColumnName("ended_at");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.Status).HasDatabaseName("IX_crises_status");
    }
}
