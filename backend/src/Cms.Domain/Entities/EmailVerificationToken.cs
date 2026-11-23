using Cms.Domain.Common;

namespace Cms.Domain.Entities;

/// <summary>
/// Single-use email verification token. Only the SHA-256 hash of the raw token
/// is persisted; the raw value exists only in the verification email link.
/// </summary>
public class EmailVerificationToken : BaseEntity
{
    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public User User { get; set; } = null!;
}
