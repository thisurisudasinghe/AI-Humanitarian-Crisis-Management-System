using Cms.Domain.Common;

namespace Cms.Domain.Entities;

/// <summary>
/// Configurable platform setting (service types, categories, thresholds, SLA hours).
/// Owned by the administration module.
/// </summary>
public class SystemSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }
}
