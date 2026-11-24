using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cms.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Cms.Infrastructure.Authentication;

public class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly IDateTimeService _dateTimeService;

    public JwtTokenService(
        IConfiguration configuration,
        IDateTimeService dateTimeService)
    {
        _configuration = configuration;
        _dateTimeService = dateTimeService;
    }

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(
        Guid userId,
        string email,
        IEnumerable<string> roles,
        Guid? organisationId = null,
        bool isEmailVerified = false)
    {
        var jwtSettings = _configuration.GetSection("Jwt");

        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is missing.");

        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];

        var expirationMinutes = jwtSettings
            .GetValue<int>("AccessTokenExpirationMinutes");

        var now = _dateTimeService.UtcNow;
        var expiresAt = now.AddMinutes(expirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("email_verified", isEmailVerified ? "true" : "false")
        };

        foreach (var role in roles.Distinct())
        {
            claims.Add(new Claim("role", role));
        }

        if (organisationId.HasValue)
        {
            claims.Add(new Claim("orgId", organisationId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secretKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler()
            .WriteToken(token);

        return (tokenString, expiresAt);
    }
}