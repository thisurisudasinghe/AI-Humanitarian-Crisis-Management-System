using System.ComponentModel.DataAnnotations;

namespace Cms.Application.DTOs.Authentication;

public sealed class ResetPasswordRequestDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    public string ConfirmPassword { get; set; } = string.Empty;
}
