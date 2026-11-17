using System.ComponentModel.DataAnnotations;
using Cms.Domain.Enums;

namespace Cms.Application.DTOs.Admin;

public class UserSummaryDto
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public Guid? OrganisationId { get; set; }

    public string? OrganisationName { get; set; }

    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();

    public UserStatus Status { get; set; } = UserStatus.Active;

    public bool IsActive => Status == UserStatus.Active;

    public DateTime? LastLoginAt { get; set; }
}

public class CreateUserRequestDto
{
    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    public Guid? OrganisationId { get; set; }

    [MinLength(1)]
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
}

public class UpdateUserRequestDto
{
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    public Guid? OrganisationId { get; set; }

    public UserStatus? Status { get; set; }
}

public class ReplaceRolesRequestDto
{
    [MinLength(1)]
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
}

public class OrganisationDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public OrganisationType OrganisationType { get; set; }

    public string? RegistrationNumber { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; }
}

public class CreateOrganisationRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public OrganisationType OrganisationType { get; set; } = OrganisationType.NonGovernmentalOrganisation;

    [MaxLength(100)]
    public string? RegistrationNumber { get; set; }

    [EmailAddress]
    [MaxLength(254)]
    public string? ContactEmail { get; set; }

    [MaxLength(20)]
    public string? ContactPhone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }
}

public class UpdateOrganisationRequestDto
{
    [MaxLength(200)]
    public string? Name { get; set; }

    public OrganisationType? OrganisationType { get; set; }

    [MaxLength(100)]
    public string? RegistrationNumber { get; set; }

    [EmailAddress]
    [MaxLength(254)]
    public string? ContactEmail { get; set; }

    [MaxLength(20)]
    public string? ContactPhone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public bool? IsActive { get; set; }
}

public class SystemSettingDto
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public class UpdateConfigRequestDto
{
    [MinLength(1)]
    public IReadOnlyCollection<SystemSettingValueDto> Settings { get; set; } = Array.Empty<SystemSettingValueDto>();
}

public class SystemSettingValueDto
{
    [Required]
    [MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Value { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
