using System.ComponentModel.DataAnnotations;

namespace Cms.Application.DTOs.Authentication;

public class RefreshRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
