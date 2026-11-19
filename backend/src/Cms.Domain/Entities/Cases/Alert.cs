using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class Alert : BaseEntity
{
    public Guid CrisisId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string AlertType { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public new DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiryDate { get; set; }

    public bool IsActive { get; set; } = true;

    public Crisis? Crisis { get; set; }
}
