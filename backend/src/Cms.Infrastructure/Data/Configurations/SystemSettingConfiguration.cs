using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings", "common");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(s => s.Key, "UX_system_settings_key")
            .IsUnique();

        builder.Property(s => s.Value)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(s => s.Description)
            .HasMaxLength(500);
    }
}
