namespace Cms.Domain.Enums.Access;

/// <summary>
/// Public-facing progress states for an anonymous emergency report. These are
/// deliberately coarse: they reveal progress without exposing internal notes,
/// staff identities or private locations.
/// </summary>
public enum GuestReportStatus
{
    Received = 1,
    UnderReview = 2,
    Assigned = 3,
    Resolved = 4,
    Closed = 5
}
