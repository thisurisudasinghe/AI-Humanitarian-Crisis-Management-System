using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Cases.Configurations;

public class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable("households", "cases");
        builder.HasKey(h => h.Id).HasName("PK_households");

        builder.Property(h => h.Id).HasColumnName("id");
        builder.Property(h => h.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(h => h.Size).HasColumnName("size").IsRequired();
        builder.Property(h => h.AccessibilityNeeds).HasColumnName("accessibility_needs").HasMaxLength(2000);
        builder.Property(h => h.ContactPhone).HasColumnName("contact_phone").HasMaxLength(500);
        builder.Property(h => h.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(h => h.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(h => h.UserId).HasDatabaseName("IX_households_user_id").IsUnique();
    }
}
