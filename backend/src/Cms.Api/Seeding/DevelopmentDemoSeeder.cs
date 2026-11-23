using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Cms.Api.Seeding;

/// <summary>
/// Development-only demo data. The demo users are upserted on every startup so
/// they keep working with whichever JWT/email keys the developer supplies via
/// user-secrets or environment variables (or the ephemeral Development keys).
/// </summary>
public sealed class DevelopmentDemoSeeder : IHostedService
{
    private const string DemoPassword = "Demo1234!";
    private static readonly Guid OrganisationId =
        Guid.Parse("0f000000-0000-0000-0000-000000000001");

    private static readonly DemoUser[] DemoUsers =
    [
        new(Guid.Parse("0b000000-0000-0000-0000-000000000001"),
            "Admin", "User", "admin@example.org", true, UserStatus.Active,
            [RoleNames.SystemAdmin]),
        new(Guid.Parse("0b000000-0000-0000-0000-000000000002"),
            "Coordinator", "User", "coordinator@example.org", true, UserStatus.Active,
            [RoleNames.EmergencyCoordinator]),
        new(Guid.Parse("0b000000-0000-0000-0000-000000000003"),
            "Case", "Manager", "casemanager@example.org", true, UserStatus.Active,
            [RoleNames.CaseManager]),
        new(Guid.Parse("0b000000-0000-0000-0000-000000000004"),
            "Logistics", "Manager", "logistics@example.org", true, UserStatus.Active,
            [RoleNames.LogisticsManager]),
        new(Guid.Parse("0b000000-0000-0000-0000-000000000005"),
            "Shelter", "Admin", "shelter@example.org", true, UserStatus.Active,
            [RoleNames.ShelterAdmin]),
        new(Guid.Parse("0b000000-0000-0000-0000-000000000006"),
            "Volunteer", "Coordinator", "volunteercoordinator@example.org", true,
            UserStatus.Active, [RoleNames.VolunteerCoordinator]),
        new(Guid.Parse("0b000000-0000-0000-0000-000000000007"),
            "Volunteer", "User", "volunteer@example.org", true, UserStatus.Active,
            [RoleNames.Volunteer]),
        new(Guid.Parse("0b000000-0000-0000-0000-000000000008"),
            "Donor", "Officer", "donor@example.org", true, UserStatus.Active,
            [RoleNames.DonorOfficer]),
        new(Guid.Parse("0b000000-0000-0000-0000-000000000009"),
            "Partner", "Officer", "partner@example.org", true, UserStatus.Active,
            [RoleNames.PartnerOfficer]),
        new(Guid.Parse("0b000000-0000-0000-0000-000000000010"),
            "Citizen", "User", "citizen@example.org", false, UserStatus.Active,
            [RoleNames.Citizen]),
        new(Guid.Parse("0b000000-0000-0000-0000-000000000011"),
            "Multi", "Role User", "multi@example.org", true, UserStatus.Active,
            [RoleNames.EmergencyCoordinator, RoleNames.CaseManager]),
        new(Guid.Parse("0b000000-0000-0000-0000-000000000012"),
            "Inactive", "User", "inactive@example.org", false, UserStatus.Inactive,
            [RoleNames.Citizen])
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DevelopmentDemoSeeder> _logger;

    public DevelopmentDemoSeeder(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DevelopmentDemoSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue("DemoSeed:Enabled", true))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

            // The test harness uses the in-memory provider and seeds its own users.
            if (!db.Database.IsRelational())
            {
                return;
            }

            var emailProtection =
                scope.ServiceProvider.GetRequiredService<EmailProtectionService>();
            var passwordHashing =
                scope.ServiceProvider.GetRequiredService<PasswordHashingService>();

            await SeedAsync(db, emailProtection, passwordHashing, cancellationToken);
        }
        catch (Exception exception)
        {
            // A fresh checkout may not have run the SQL schema scripts yet. The
            // API should still start so the developer can create the database.
            _logger.LogWarning(
                exception,
                "Development demo users were not seeded. Run the SQL schema scripts first.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedAsync(
        CmsDbContext db,
        EmailProtectionService emailProtection,
        PasswordHashingService passwordHashing,
        CancellationToken cancellationToken)
    {
        var roles = (await db.Roles.ToListAsync(cancellationToken))
            .ToDictionary(role => role.Name, StringComparer.Ordinal);

        foreach (var roleName in RoleNames.All)
        {
            if (roles.ContainsKey(roleName))
            {
                continue;
            }

            var role = new Role
            {
                Id = RoleId(roleName),
                Name = roleName,
                Description = roleName
            };
            db.Roles.Add(role);
            roles[roleName] = role;
        }

        var organisation = await db.Organisations
            .SingleOrDefaultAsync(
                item => item.Id == OrganisationId || item.Name == "Lead Agency",
                cancellationToken);

        if (organisation is null)
        {
            organisation = new Organisation
            {
                Id = OrganisationId,
                Name = "Lead Agency",
                OrganisationType = OrganisationType.Government,
                RegistrationNumber = "DEMO-ORG-001",
                Address = "Demo Headquarters",
                IsActive = true
            };
            db.Organisations.Add(organisation);
        }

        foreach (var demo in DemoUsers)
        {
            var user = await db.Users
                .Include(item => item.UserRoles)
                .SingleOrDefaultAsync(item => item.Id == demo.Id, cancellationToken);

            if (user is null)
            {
                user = new User { Id = demo.Id };
                db.Users.Add(user);
            }

            var emailHash = emailProtection.HashEmail(demo.Email);
            var emailInUse = await db.Users.AnyAsync(
                item => item.Id != demo.Id && item.EmailHash == emailHash,
                cancellationToken);

            if (emailInUse)
            {
                _logger.LogWarning(
                    "Skipping demo user {Email}: another user already uses that email.",
                    demo.Email);
                continue;
            }

            user.FirstName = demo.FirstName;
            user.LastName = demo.LastName;
            user.PhoneNumber = null;
            user.EmailEncrypted = emailProtection.EncryptEmail(demo.Email);
            user.EmailHash = emailHash;
            user.PasswordHash = passwordHashing.HashPassword(user, DemoPassword);
            user.IsEmailVerified = true;
            user.Status = demo.Status;
            user.OrganisationId = demo.InLeadAgency ? organisation.Id : null;
            user.UpdatedAt = DateTime.UtcNow;

            var desiredRoleIds = demo.Roles
                .Select(roleName => roles[roleName].Id)
                .ToHashSet();

            foreach (var existing in user.UserRoles
                .Where(item => !desiredRoleIds.Contains(item.RoleId))
                .ToList())
            {
                user.UserRoles.Remove(existing);
            }

            foreach (var roleId in desiredRoleIds)
            {
                if (user.UserRoles.All(item => item.RoleId != roleId))
                {
                    user.UserRoles.Add(new UserRole
                    {
                        UserId = user.Id,
                        RoleId = roleId
                    });
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Guid RoleId(string roleName) => roleName switch
    {
        RoleNames.Citizen => Guid.Parse("0a000000-0000-0000-0000-000000000001"),
        RoleNames.EmergencyCoordinator => Guid.Parse("0a000000-0000-0000-0000-000000000002"),
        RoleNames.CaseManager => Guid.Parse("0a000000-0000-0000-0000-000000000003"),
        RoleNames.LogisticsManager => Guid.Parse("0a000000-0000-0000-0000-000000000004"),
        RoleNames.ShelterAdmin => Guid.Parse("0a000000-0000-0000-0000-000000000005"),
        RoleNames.VolunteerCoordinator => Guid.Parse("0a000000-0000-0000-0000-000000000006"),
        RoleNames.Volunteer => Guid.Parse("0a000000-0000-0000-0000-000000000007"),
        RoleNames.DonorOfficer => Guid.Parse("0a000000-0000-0000-0000-000000000008"),
        RoleNames.PartnerOfficer => Guid.Parse("0a000000-0000-0000-0000-000000000009"),
        RoleNames.SystemAdmin => Guid.Parse("0a000000-0000-0000-0000-000000000010"),
        RoleNames.Donor => Guid.Parse("0a000000-0000-0000-0000-000000000011"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(roleName), roleName, "Unknown system role.")
    };

    private sealed record DemoUser(
        Guid Id,
        string FirstName,
        string LastName,
        string Email,
        bool InLeadAgency,
        UserStatus Status,
        string[] Roles);
}
