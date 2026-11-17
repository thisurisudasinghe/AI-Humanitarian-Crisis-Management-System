using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "identity");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.EmailEncrypted)
            .HasMaxLength(1000);

        builder.Property(u => u.EmailHash)
            .HasMaxLength(64);

        builder.HasIndex(u => u.EmailHash, "UX_users_email_hash")
            .IsUnique()
            .HasFilter("[email_hash] IS NOT NULL");

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.HasOne(u => u.Organisation)
            .WithMany()
            .HasForeignKey(u => u.OrganisationId)
            .HasConstraintName("FK_users_organisations")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId)
            .HasConstraintName("FK_user_roles_users")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
