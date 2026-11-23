
namespace Cms.Application.DTOs;

public class CurrentUserDto
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public IReadOnlyCollection<string> Roles { get; set; }
        = Array.Empty<string>();

    public bool IsEmailVerified { get; set; }
}
