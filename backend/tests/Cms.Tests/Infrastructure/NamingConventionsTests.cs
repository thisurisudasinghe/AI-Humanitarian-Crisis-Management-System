using Cms.Infrastructure.Data;

namespace Cms.Tests.Infrastructure;

public class NamingConventionsTests
{
    [Theory]
    [InlineData("Users", "users")]
    [InlineData("OrganisationType", "organisation_type")]
    [InlineData("EmailHash", "email_hash")]
    [InlineData("RefreshTokens", "refresh_tokens")]
    [InlineData("UserID", "user_id")]
    [InlineData("IPAddress", "ip_address")]
    [InlineData("user_roles", "user_roles")]
    public void ToSnakeCase_maps_pascal_case_names(string input, string expected)
    {
        Assert.Equal(expected, NamingConventions.ToSnakeCase(input));
    }

    [Fact]
    public void ToSnakeCase_handles_empty_input()
    {
        Assert.Equal(string.Empty, NamingConventions.ToSnakeCase(string.Empty));
    }
}
