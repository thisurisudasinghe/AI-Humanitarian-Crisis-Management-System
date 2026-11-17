using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class Attachment : BaseEntity
{
    public Guid RequestId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileUrl { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public long FileSize { get; set; }

    public string? UploadedByUserId { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Request? Request { get; set; }
}
