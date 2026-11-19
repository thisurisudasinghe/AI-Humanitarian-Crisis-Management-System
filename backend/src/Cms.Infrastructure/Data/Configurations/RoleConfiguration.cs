using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", "identity");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(r => r.Name, "UX_roles_name")
            .IsUnique();

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        // Roles are seeded by db/99_seed_users.sql, not by EF HasData.
    }
}
