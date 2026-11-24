using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("EmailVerificationTokens", "identity");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(token => token.TokenHash, "UX_email_verification_tokens_token_hash")
            .IsUnique();

        // Historical (consumed/revoked) rows are retained for audit. The SQL
        // script enforces one ACTIVE token per user with a filtered unique index;
        // the service also revokes the previous token before inserting a new one.
        builder.HasIndex(token => token.UserId, "IX_email_verification_tokens_user_created");

        builder.Property(token => token.ExpiresAt)
            .IsRequired();

        // Optimistic concurrency makes redemption single-use even when two
        // requests load the same token simultaneously.
        builder.Property(token => token.ConsumedAt)
            .IsConcurrencyToken();

        builder.Property(token => token.RevokedAt)
            .IsConcurrencyToken();

        builder.HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .HasConstraintName("FK_email_verification_tokens_users")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
