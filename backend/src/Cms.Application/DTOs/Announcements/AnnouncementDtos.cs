namespace Cms.Application.DTOs.Announcements;

/// <summary>Audience selection for an emergency announcement.</summary>
public class AnnouncementAudienceDto
{
    /// <summary>Explicit recipient user ids.</summary>
    public IReadOnlyList<Guid> UserIds { get; set; } = Array.Empty<Guid>();

    /// <summary>Frozen role names; all active users holding any role are included.</summary>
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    /// <summary>Every active user in the platform.</summary>
    public bool AllActiveUsers { get; set; }
}

public class CreateAnnouncementRequest
{
    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public AnnouncementAudienceDto Audience { get; set; } = new();

    public string Channel { get; set; } = "InApp";

    /// <summary>Template language for the draft. English and Spanish are seeded.</summary>
    public string Language { get; set; } = "en";

    /// <summary>Optional client key; a generated one is used when omitted.</summary>
    public string? IdempotencyKey { get; set; }
}

public class ApproveAnnouncementRequest
{
    public string? Note { get; set; }
}

public class RejectAnnouncementRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class AnnouncementDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public AnnouncementAudienceDto Audience { get; set; } = new();

    public string Channel { get; set; } = string.Empty;

    public string Language { get; set; } = "en";

    public string Status { get; set; } = string.Empty;

    public Guid RequestedByUserId { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime? DispatchedAt { get; set; }

    public int RecipientCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
