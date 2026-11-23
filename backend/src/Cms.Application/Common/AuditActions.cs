namespace Cms.Application.Common;

/// <summary>
/// Canonical audit action names. Use these constants instead of raw strings
/// so the audit trail stays filterable.
/// </summary>
public static class AuditActions
{
    // Identity
    public const string UserRegistered = "User.Registered";
    public const string UserLoggedIn = "User.LoggedIn";
    public const string UserLoggedOut = "User.LoggedOut";
    public const string UserRoleChanged = "User.RoleChanged";
    public const string UserUpdated = "User.Updated";
    public const string UserDeactivated = "User.Deactivated";
    public const string PasswordResetRequested = "User.PasswordResetRequested";
    public const string PasswordResetCompleted = "User.PasswordResetCompleted";

    // Administration
    public const string OrganisationCreated = "Organisation.Created";
    public const string OrganisationUpdated = "Organisation.Updated";
    public const string OrganisationDeactivated = "Organisation.Deactivated";
    public const string ConfigUpdated = "Config.Updated";
    public const string OrganisationReactivated = "Organisation.Reactivated";
    public const string OutboxRetried = "Outbox.Retried";

    // Evidence files
    public const string FileUploaded = "File.Uploaded";

    // Cases
    public const string RequestSubmitted = "Request.Submitted";
    public const string RequestStatusChanged = "Request.StatusChanged";

    // Governance
    public const string DecisionRecorded = "Decision.Recorded";
    public const string AnnouncementApproved = "Announcement.Approved";

    // Logistics, shelter and volunteers
    public const string AllocationCreated = "Allocation.Created";
    public const string DispatchCreated = "Dispatch.Created";
    public const string ShelterAllocated = "Shelter.Allocated";
    public const string VolunteerTaskAssigned = "Volunteer.TaskAssigned";

    // Donations (P2)
    public const string DonorCreated = "Donor.Created";
    public const string DonorUpdated = "Donor.Updated";
    public const string CampaignCreated = "Campaign.Created";
    public const string CampaignUpdated = "Campaign.Updated";
    public const string CampaignExpenseRecorded = "Campaign.ExpenseRecorded";
    public const string DonationRecorded = "Donation.Recorded";
    public const string DonationAllocated = "Donation.Allocated";
    public const string DonationFlagged = "Donation.Flagged";
    public const string DonationAllocationFlagged = "Donation.AllocationFlagged";
}
