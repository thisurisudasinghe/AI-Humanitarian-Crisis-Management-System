using System.ComponentModel.DataAnnotations;

namespace Cms.Application.DTOs.Authentication;

public sealed class ForgotPasswordRequestDto
{
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;
}
