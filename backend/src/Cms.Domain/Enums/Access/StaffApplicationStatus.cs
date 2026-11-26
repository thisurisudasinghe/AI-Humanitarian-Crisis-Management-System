namespace Cms.Domain.Enums.Access;

/// <summary>
/// Lifecycle of an authenticated user's request for operational staff access.
/// The submitted role remains application data until an authorized reviewer
/// explicitly approves it.
/// </summary>
public enum StaffApplicationStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}
