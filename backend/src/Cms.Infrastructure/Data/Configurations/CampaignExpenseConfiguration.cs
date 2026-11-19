using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class CampaignExpenseConfiguration
    : IEntityTypeConfiguration<CampaignExpense>
{
    public void Configure(EntityTypeBuilder<CampaignExpense> builder)
    {
        builder.ToTable("CampaignExpenses", "don");

        builder.HasKey(expense => expense.Id);

        builder.Property(expense => expense.Description)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(expense => expense.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(expense => expense.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(expense => expense.SpentAt)
            .IsRequired();

        builder.HasOne(expense => expense.Campaign)
            .WithMany(campaign => campaign.Expenses)
            .HasForeignKey(expense => expense.CampaignId)
            .HasConstraintName("FK_campaign_expenses_campaigns")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(
            expense => expense.CampaignId,
            "IX_campaign_expenses_campaign_id");

        builder.HasIndex(
            expense => expense.SpentAt,
            "IX_campaign_expenses_spent_at");
    }
}
