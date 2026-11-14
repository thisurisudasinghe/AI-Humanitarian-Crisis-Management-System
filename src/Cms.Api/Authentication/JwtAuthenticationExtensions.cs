using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Cms.Api.Authentication;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
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

                options.TokenValidationParameters =
                    new TokenValidationParameters
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

        services.AddAuthorization();

        return services;
    }
}