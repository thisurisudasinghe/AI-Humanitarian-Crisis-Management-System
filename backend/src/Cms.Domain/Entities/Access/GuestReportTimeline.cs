using Cms.Domain.Enums.Access;

namespace Cms.Domain.Entities.Access;

/// <summary>
/// Append-only progress entries exposed through the secure guest tracking
/// bearer token. Notes are short operational summaries, never internal records.
/// </summary>
public class GuestReportTimeline
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReportId { get; set; }

    public GuestReportStatus Status { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
}
