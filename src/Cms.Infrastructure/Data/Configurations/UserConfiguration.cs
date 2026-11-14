using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

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

        builder.HasIndex(u => u.EmailHash)
            .IsUnique()
            .HasFilter("[EmailHash] IS NOT NULL");

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
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId);
    }
}