using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cms.Domain.Enums.Cases;

namespace Cms.Application.Cases.DTOs;

public class CreateRequestDto
{
    [Required, JsonConverter(typeof(JsonStringEnumConverter))]
    public Category Category { get; set; }

    [Required, JsonConverter(typeof(JsonStringEnumConverter))]
    public Urgency Urgency { get; set; }

    [Required, StringLength(2000, MinimumLength = 5)]
    public string Description { get; set; } = string.Empty;

    [Range(-90, 90)] public decimal Lat { get; set; }
    [Range(-180, 180)] public decimal Lng { get; set; }
    public string? Address { get; set; }

    [Range(1, 100)] public int HouseholdSize { get; set; } = 1;
    public string? AccessibilityNeeds { get; set; }
    public Guid? CrisisId { get; set; }
    public Guid? AreaId { get; set; }
    public IReadOnlyList<Guid>? EvidenceAttachmentIds { get; set; }
}

public class UpdateRequestDto
{
    [StringLength(2000, MinimumLength = 5)] public string? Description { get; set; }
    [Range(-90, 90)] public decimal? Lat { get; set; }
    [Range(-180, 180)] public decimal? Lng { get; set; }
    [Range(1, 100)] public int? HouseholdSize { get; set; }
    public string? AccessibilityNeeds { get; set; }

    /// <summary>Staff-only assignment through the documented PATCH /requests/{id} route.</summary>
    public Guid? AssignedToUserId { get; set; }
}

public class RequestFilter
{
    public Guid? RequesterUserId { get; set; }
    public RequestStatus? Status { get; set; }
    public IReadOnlyList<RequestStatus>? Statuses { get; set; }
    public Urgency? Urgency { get; set; }
    public Category? Category { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? CrisisId { get; set; }
    public Guid? AssignedTo { get; set; }
    public bool? Overdue { get; set; }
    public string? Q { get; set; }
}

public class RequestListItemDto
{
    public Guid Id { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))] public Category Category { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))] public Urgency Urgency { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))] public RequestStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? AreaId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int SlaAgeHours { get; set; }
    public bool IsOverdue { get; set; }
}

public class RequestDetailDto : RequestListItemDto
{
    public Guid RequesterUserId { get; set; }
    public Guid? CrisisId { get; set; }
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
    public int HouseholdSize { get; set; }
    public string? AccessibilityNeeds { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public Guid? MergedIntoRequestId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? TriagedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public IReadOnlyList<AttachmentMetadataDto> Attachments { get; set; } = Array.Empty<AttachmentMetadataDto>();
    public IReadOnlyList<RequestTimelineEntryDto> Timeline { get; set; } = Array.Empty<RequestTimelineEntryDto>();
}

public class RequestTimelineEntryDto
{
    public Guid Id { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))] public RequestStatus FromStatus { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))] public RequestStatus ToStatus { get; set; }
    public string? ActorDisplayName { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TransitionRequestDto
{
    [Required, JsonConverter(typeof(JsonStringEnumConverter))]
    public RequestStatus To { get; set; }

    [StringLength(1000)] public string? Reason { get; set; }
    public Guid? DecisionId { get; set; }
}

public class MergeRequestDto
{
    [Required] public Guid TargetRequestId { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }
}

public class AssignRequestDto
{
    [Required] public Guid AssignedToUserId { get; set; }
}

public class AttachmentMetadataDto
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFeedbackDto
{
    [Required, StringLength(2000, MinimumLength = 2)] public string Comment { get; set; } = string.Empty;
}

public class UploadedFile
{
    public Stream Content { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}

public class AttachmentDownload
{
    public Stream Content { get; set; } = Stream.Null;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}
