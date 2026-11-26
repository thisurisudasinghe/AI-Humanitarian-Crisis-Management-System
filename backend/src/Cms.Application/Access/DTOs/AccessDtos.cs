using System.ComponentModel.DataAnnotations;

namespace Cms.Application.Access.DTOs;

/// <summary>
/// Authenticated application for an operational staff role. The requested role
/// is treated as review data only; it grants no permission until approved.
/// </summary>
public class SubmitStaffApplicationDto
{
    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Organization { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Designation { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string RequestedRole { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

public class StaffApplicationReceiptDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}

public class StaffApplicationDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string RequestedRole { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class ReviewStaffApplicationDto
{
    public bool Approved { get; set; }

    [StringLength(1000)]
    public string? Reason { get; set; }
}

/// <summary>
/// Anonymous emergency report. ClientToken must be a freshly generated UUID;
/// it is returned once as the tracking token and stored only as a hash.
/// </summary>
public class CreateGuestReportDto
{
    [Required, StringLength(100)]
    public string IncidentType { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Location { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ContactInfo { get; set; }

    [Required, StringLength(64)]
    public string ClientToken { get; set; } = string.Empty;
}

public class GuestReportReceiptDto
{
    public string Reference { get; set; } = string.Empty;
    public string TrackingToken { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}

public class GuestReportTrackingDto
{
    public string Reference { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyList<GuestReportTimelineDto> Timeline { get; set; } = [];
}

public class GuestReportTimelineDto
{
    public string Status { get; set; } = string.Empty;
    public DateTime At { get; set; }
}

public class TrackGuestReportDto
{
    [Required, StringLength(64)]
    public string Token { get; set; } = string.Empty;
}

public class GuestReportSummaryDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateGuestReportStatusDto
{
    [Required, StringLength(40)]
    public string Status { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Note { get; set; }
}
