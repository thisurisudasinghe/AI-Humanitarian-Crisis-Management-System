using System.ComponentModel.DataAnnotations;

namespace Cms.Application.DTOs.Authentication;

public class VerifyEmailRequestDto
{
    [Required]
    [StringLength(200)]
    public string Token { get; set; } = string.Empty;
}

public class ResendVerificationRequestDto
{
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Deliberately minimal response. Never contains a token, email address or
/// account existence information.
/// </summary>
public class EmailVerificationResponseDto
{
    public bool IsEmailVerified { get; set; }

    public bool AlreadyVerified { get; set; }

    public string Message { get; set; } = string.Empty;
}
