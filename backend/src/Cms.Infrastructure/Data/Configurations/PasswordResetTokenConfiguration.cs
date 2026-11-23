using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens", "identity");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(token => token.TokenHash, "UX_password_reset_tokens_token_hash")
            .IsUnique();

        builder.HasIndex(token => token.UserId, "IX_password_reset_tokens_user_id");

        builder.Property(token => token.ExpiresAt)
            .IsRequired();

        builder.Property(token => token.UsedAt);

        builder.HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .HasConstraintName("FK_password_reset_tokens_users")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
