using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Cms.Application.DTOs.Authentication;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Cms.Tests.Integration;

public static class TestSecrets
{
    public const string JwtSecret = "cms-test-jwt-secret-key-with-at-least-thirty-two-bytes";
    public static readonly string EmailEncryptionKey = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray());
    public static readonly string EmailHashKey = Convert.ToBase64String(Enumerable.Repeat((byte)13, 32).ToArray());
    public const string Password = "Demo1234!";
}

/// <summary>
/// Boots the real API pipeline against an isolated in-memory database so tests
/// exercise routing, authentication, validation, middleware and controllers.
/// </summary>
public sealed class CmsWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"cms-tests-{Guid.NewGuid()}";
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(),
        "cms-test-files",
        Guid.NewGuid().ToString("N"));
    private readonly SemaphoreSlim _seedLock = new(1, 1);
    private bool _seeded;

    public const string AdminEmail = "admin@tests.local";
    public const string OfficerEmail = "officer@tests.local";
    public const string CitizenEmail = "citizen@tests.local";
    public const string InactiveEmail = "inactive@tests.local";

    /// <summary>Per-factory upload directory, isolated from the real content root.</summary>
    public string StorageRoot => _storageRoot;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=unused-for-in-memory-tests",
                ["Jwt:Issuer"] = "Cms.Api",
                ["Jwt:Audience"] = "Cms.Client",
                ["Jwt:SecretKey"] = TestSecrets.JwtSecret,
                ["Jwt:AccessTokenExpirationMinutes"] = "15",
                ["Jwt:RefreshTokenExpirationDays"] = "7",
                ["EmailProtection:EncryptionKey"] = TestSecrets.EmailEncryptionKey,
                ["EmailProtection:HashKey"] = TestSecrets.EmailHashKey,
                ["FileStorage:RootPath"] = _storageRoot
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var dbContextOptions = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions) ||
                    descriptor.ServiceType == typeof(DbContextOptions<CmsDbContext>) ||
                    descriptor.ServiceType == typeof(IDbContextOptionsConfiguration<CmsDbContext>))
                .ToList();

            foreach (var descriptor in dbContextOptions)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<CmsDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task EnsureSeededAsync()
    {
        if (_seeded)
        {
            return;
        }

        await _seedLock.WaitAsync();

        try
        {
            if (_seeded)
            {
                return;
            }

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            var emailProtection = scope.ServiceProvider.GetRequiredService<EmailProtectionService>();
            var passwordHashing = scope.ServiceProvider.GetRequiredService<PasswordHashingService>();

            var roles = RoleNames.All
                .Select(name => new Role { Name = name })
                .ToList();
            db.Roles.AddRange(roles);

            var organisation = new Organisation
            {
                Name = "Test Lead Agency",
                OrganisationType = OrganisationType.Government,
                IsActive = true
            };
            db.Organisations.Add(organisation);

            AddUser(db, emailProtection, passwordHashing, AdminEmail, "System Admin",
                [RoleNames.SystemAdmin], organisation.Id);
            AddUser(db, emailProtection, passwordHashing, OfficerEmail, "Field Officer",
                [RoleNames.PartnerOfficer, RoleNames.CaseManager], organisation.Id);
            AddUser(db, emailProtection, passwordHashing, CitizenEmail, "Test Citizen",
                [RoleNames.Citizen], null);
            AddUser(db, emailProtection, passwordHashing, InactiveEmail, "Inactive User",
                [RoleNames.Citizen], null, UserStatus.Inactive);

            await db.SaveChangesAsync();
            _seeded = true;
        }
        finally
        {
            _seedLock.Release();
        }
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        await EnsureSeededAsync();

        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = TestSecrets.Password
        });

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>()
            ?? throw new InvalidOperationException("Login response was empty.");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return client;
    }

    public async Task<AuthResponseDto> LoginAsync(string email)
    {
        await EnsureSeededAsync();

        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = TestSecrets.Password
        });

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AuthResponseDto>()
            ?? throw new InvalidOperationException("Login response was empty.");
    }

    private static void AddUser(
        CmsDbContext db,
        EmailProtectionService emailProtection,
        PasswordHashingService passwordHashing,
        string email,
        string fullName,
        IEnumerable<string> roleNames,
        Guid? organisationId,
        UserStatus status = UserStatus.Active)
    {
        var user = new User
        {
            FirstName = fullName.Split(' ')[0],
            LastName = string.Join(' ', fullName.Split(' ').Skip(1)),
            EmailEncrypted = emailProtection.EncryptEmail(email),
            EmailHash = emailProtection.HashEmail(email),
            PasswordHash = passwordHashing.HashPassword(new User(), TestSecrets.Password),
            OrganisationId = organisationId,
            Status = status,
            IsEmailVerified = true
        };

        var names = roleNames.ToArray();
        user.UserRoles = db.Roles
            .Local
            .Where(role => names.Contains(role.Name, StringComparer.Ordinal))
            .Select(role => new UserRole { RoleId = role.Id, UserId = user.Id })
            .ToList();

        db.Users.Add(user);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            if (Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup of the per-factory upload directory.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup of the per-factory upload directory.
        }
    }
}
