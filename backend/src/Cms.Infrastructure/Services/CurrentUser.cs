using System.Security.Claims;
using Cms.Application.Contracts;
using Microsoft.AspNetCore.Http;

namespace Cms.Infrastructure.Services;

/// <summary>
/// Resolves the current principal from the JWT access token
/// (claims: sub, orgId, email, one "role" claim per role).
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal =>
        _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue("sub")
                ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid? OrgId
    {
        get
        {
            var value = Principal?.FindFirstValue("orgId")
                ?? Principal?.FindFirstValue("org_id");

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email =>
        Principal?.FindFirstValue("email")
        ?? Principal?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyCollection<string> Roles =>
        Principal?
            .FindAll("role")
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray()
        ?? [];

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return Roles.Contains(role, StringComparer.Ordinal);
    }
}
