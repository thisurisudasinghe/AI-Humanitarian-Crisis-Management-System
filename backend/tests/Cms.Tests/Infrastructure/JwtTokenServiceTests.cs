using System.IdentityModel.Tokens.Jwt;
using Cms.Domain.Constants;
using Cms.Infrastructure.Authentication;
using Cms.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace Cms.Tests.Infrastructure;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService() =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SecretKey"] = new string('k', 64),
                    ["Jwt:Issuer"] = "Cms.Api",
                    ["Jwt:Audience"] = "Cms.Client",
                    ["Jwt:AccessTokenExpirationMinutes"] = "15"
                })
                .Build(),
            new DateTimeService());

    [Fact]
    public void Access_token_carries_sub_role_and_org_claims()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();
        var organisationId = Guid.NewGuid();

        var (token, _) = service.GenerateAccessToken(
            userId,
            "officer@example.org",
            [RoleNames.CaseManager, RoleNames.PartnerOfficer],
            organisationId);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(userId.ToString(), parsed.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal(organisationId.ToString(), parsed.Claims.Single(c => c.Type == "orgId").Value);
        Assert.Equal(
            new[] { RoleNames.CaseManager, RoleNames.PartnerOfficer }.Order(),
            parsed.Claims.Where(c => c.Type == "role").Select(c => c.Value).Order());
    }

    [Fact]
    public void Access_token_omits_org_claim_when_no_organisation_is_set()
    {
        var service = CreateService();

        var (token, _) = service.GenerateAccessToken(
            Guid.NewGuid(),
            "citizen@example.org",
            [RoleNames.Citizen]);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.DoesNotContain(parsed.Claims, claim => claim.Type == "orgId");
    }
}
