using Cms.Domain.Common;
using Cms.Domain.Enums.Access;

namespace Cms.Domain.Entities.Access;

/// <summary>
/// An authenticated public account's application for operational staff access.
/// Applicant details are stored protected; only authorized reviewers can read
/// the decrypted values through the service layer.
/// </summary>
public class StaffAccessApplication : BaseEntity
{
    public Guid ApplicantUserId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Organization { get; set; } = string.Empty;

    public string Designation { get; set; } = string.Empty;

    public string RequestedRole { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public StaffApplicationStatus Status { get; set; } = StaffApplicationStatus.Pending;

    public DateTime SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewerUserId { get; set; }

    public string? RejectionReason { get; set; }
}
