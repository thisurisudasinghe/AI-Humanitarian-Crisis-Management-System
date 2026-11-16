using Cms.Domain.Common;
using Cms.Domain.Enums;

namespace Cms.Domain.Entities;

public class Organisation : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public OrganisationType OrganisationType { get; set; }

    public string? RegistrationNumber { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}