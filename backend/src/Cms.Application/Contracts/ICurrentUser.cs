namespace Cms.Application.Contracts;

/// <summary>
/// Current principal, resolved from the JWT access token.
/// Implemented by Member 1 (M1).
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid? OrgId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}
