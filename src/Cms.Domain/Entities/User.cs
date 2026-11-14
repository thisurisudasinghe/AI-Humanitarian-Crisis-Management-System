using Cms.Domain.Common;
using Cms.Domain.Enums;

namespace Cms.Domain.Entities;

public class User : BaseEntity
{
    public Guid? OrganisationId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? EmailEncrypted { get; set; }

    public string? EmailHash { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsEmailVerified { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTime? LastLoginAt { get; set; }

    public Organisation? Organisation { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}