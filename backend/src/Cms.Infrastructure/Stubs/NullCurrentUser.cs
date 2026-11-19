using Cms.Application.Contracts;

namespace Cms.Infrastructure.Stubs;

/// <summary>Anonymous principal, for background work and tests.</summary>
public sealed class NullCurrentUser : ICurrentUser
{
    public Guid? UserId => null;

    public Guid? OrgId => null;

    public string? Email => null;

    public IReadOnlyCollection<string> Roles => [];

    public bool IsAuthenticated => false;

    public bool IsInRole(string role) => false;
}
