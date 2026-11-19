using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaigns", "don");

        builder.HasKey(campaign => campaign.Id);

        builder.Property(campaign => campaign.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(campaign => campaign.Description)
            .HasMaxLength(2000);

        builder.Property(campaign => campaign.GoalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(campaign => campaign.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(campaign => campaign.StartsAt)
            .IsRequired();

        builder.Property(campaign => campaign.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.HasIndex(campaign => campaign.Status, "IX_campaigns_status");
        builder.HasIndex(campaign => campaign.StartsAt, "IX_campaigns_starts_at");
    }
}
