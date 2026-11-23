using System.ComponentModel.DataAnnotations;

namespace Cms.Application.DTOs.Portal;

/// <summary>
/// Requested public participation role. The value is only a request: the
/// identity service validates it against a server-side allowlist.
/// </summary>
public class EnrollPublicRoleRequestDto
{
    [Required]
    [MaxLength(50)]
    public string Purpose { get; set; } = string.Empty;
}
