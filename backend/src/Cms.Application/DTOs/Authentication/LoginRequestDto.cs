using System.ComponentModel.DataAnnotations;

namespace Cms.Application.DTOs.Authentication;

public class LoginRequestDto
{
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}