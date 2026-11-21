using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations.AI;

public class RecommendationConfiguration
    : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.ToTable("Recommendations", "ai");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.PayloadJson)
            .IsRequired();

        builder.Property(x => x.Confidence)
            .HasPrecision(5, 4);

        builder.Property(x => x.Rationale)
            .IsRequired();

        builder.Property(x => x.Source)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(30)
            .HasDefaultValue(Cms.Domain.Constants.RecommendationStatuses.Pending);

        builder.HasIndex(x => new { x.TargetId, x.Type });
    }
}