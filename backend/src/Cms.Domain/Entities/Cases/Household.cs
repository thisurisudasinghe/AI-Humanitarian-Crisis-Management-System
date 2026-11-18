using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class Household : BaseEntity
{
    public Guid UserId { get; set; }
    public int Size { get; set; }
    public string? AccessibilityNeeds { get; set; }
    public string? ContactPhone { get; set; }
}
