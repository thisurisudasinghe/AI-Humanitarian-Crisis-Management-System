namespace Cms.Domain.Enums;

/// <summary>
/// Recorded state of a donation. Suspicious donations keep their status and
/// carry the separate <c>IsSuspicious</c> flag so the financial trail is never
/// rewritten by a review decision.
/// </summary>
public enum DonationStatus
{
    Received = 1,
    Allocated = 2,
    Cancelled = 3
}
