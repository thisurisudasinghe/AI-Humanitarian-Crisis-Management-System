using Cms.Domain.Common;
using Cms.Domain.Enums.Access;

namespace Cms.Domain.Entities.Access;

/// <summary>
/// An anonymous emergency report. The bearer tracking token is never stored in
/// plaintext; only its SHA-256 hash is persisted. The report is never published
/// to the public map or alert feeds.
/// </summary>
public class GuestReport : BaseEntity
{
    public string Reference { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public string IncidentType { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? ContactInfo { get; set; }

    public GuestReportStatus Status { get; set; } = GuestReportStatus.Received;

    public DateTime SubmittedAt { get; set; }

    public ICollection<GuestReportTimeline> Timeline { get; set; } = new List<GuestReportTimeline>();
}
