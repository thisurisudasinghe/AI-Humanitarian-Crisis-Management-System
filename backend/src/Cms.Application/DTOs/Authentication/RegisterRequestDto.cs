
using System.ComponentModel.DataAnnotations;

namespace Cms.Application.DTOs.Authentication;

public class RegisterRequestDto
{
    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Optional public account purpose. Server validates against the safe
    /// public self-registration allowlist and never grants privileged roles.
    /// </summary>
    [MaxLength(50)]
    public string? AccountPurpose { get; set; }
}
