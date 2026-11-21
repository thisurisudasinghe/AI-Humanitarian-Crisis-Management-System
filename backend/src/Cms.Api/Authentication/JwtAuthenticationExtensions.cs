using System.Text;
using Cms.Domain.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Cms.Api.Authentication;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    JwtBearerDefaults.AuthenticationScheme;

                options.DefaultChallengeScheme =
                    JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
            });

        // Resolve the secret through DI so configuration providers added by the
        // test host (or by a deployment after service registration) are honoured.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration>((options, configuration) =>
            {
                var jwtSettings = configuration
                    .GetSection(JwtSettings.SectionName)
                    .Get<JwtSettings>()
                    ?? throw new InvalidOperationException(
                        "JWT settings are missing.");

                if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey) ||
                    Encoding.UTF8.GetByteCount(jwtSettings.SecretKey) < 32)
                {
                    throw new InvalidOperationException(
                        "JWT SecretKey must contain at least 32 UTF-8 bytes.");
                }

                if (string.IsNullOrWhiteSpace(jwtSettings.Issuer) ||
                    string.IsNullOrWhiteSpace(jwtSettings.Audience))
                {
                    throw new InvalidOperationException(
                        "JWT Issuer and Audience must be configured.");
                }

                var signingKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.SecretKey));

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,

                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,

                    ValidAlgorithms =
                        new[] { SecurityAlgorithms.HmacSha256 },

                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization(options =>
        {
            var staffRoles = RoleNames.All
                .Where(role => role != RoleNames.Citizen)
                .ToArray();

            options.AddPolicy("StaffOnly", policy =>
                policy.RequireRole(staffRoles));

            options.AddPolicy("AdminOnly", policy =>
                policy.RequireRole(RoleNames.SystemAdmin));

            options.AddPolicy("CoordinatorOnly", policy =>
                policy.RequireRole(RoleNames.EmergencyCoordinator));

            options.AddPolicy("DataOfficer", policy =>
                policy.RequireRole(
                    RoleNames.EmergencyCoordinator,
                    RoleNames.CaseManager));

            options.AddPolicy("KpiViewer", policy =>
                policy.RequireRole(
                    RoleNames.SystemAdmin,
                    RoleNames.EmergencyCoordinator,
                    RoleNames.PartnerOfficer));

            // Donation management (M8). Donor Officers record money and goods;
            // coordinators and partner officers may read the transparency data.
            options.AddPolicy("DonationManager", policy =>
                policy.RequireRole(
                    RoleNames.SystemAdmin,
                    RoleNames.DonorOfficer));

            options.AddPolicy("DonationViewer", policy =>
                policy.RequireRole(
                    RoleNames.SystemAdmin,
                    RoleNames.DonorOfficer,
                    RoleNames.EmergencyCoordinator,
                    RoleNames.PartnerOfficer));

            // Endpoints must opt in to being public with [AllowAnonymous].
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
