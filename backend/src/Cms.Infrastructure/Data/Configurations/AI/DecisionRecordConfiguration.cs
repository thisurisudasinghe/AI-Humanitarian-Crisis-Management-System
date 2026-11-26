using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations.AI;

public class DecisionRecordConfiguration
    : IEntityTypeConfiguration<DecisionRecord>
{
    public void Configure(EntityTypeBuilder<DecisionRecord> builder)
    {
        builder.ToTable("DecisionRecords", "ai");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Outcome)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.FinalPayloadJson);

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.DecidedAt)
            .IsRequired();

        builder.HasIndex(x => x.RecommendationId).IsUnique();

        builder.HasOne<Recommendation>()
            .WithMany()
            .HasForeignKey(x => x.RecommendationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}