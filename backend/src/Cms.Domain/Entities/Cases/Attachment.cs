using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class Attachment : BaseEntity
{
    public Guid RequestId { get; set; }
    public string StoredFileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public Guid UploadedByUserId { get; set; }
}
