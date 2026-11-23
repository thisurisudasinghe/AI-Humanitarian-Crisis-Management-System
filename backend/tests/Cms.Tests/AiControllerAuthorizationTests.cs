using System.Reflection;
using Cms.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace Cms.Tests;

public sealed class AiControllerAuthorizationTests
{
    [Fact]
    public void ChatAction_AllowsGuests_AndUsesPublicRateLimit()
    {
        var method = typeof(AiController).GetMethod(nameof(AiController.Chat));

        Assert.NotNull(method);
        Assert.NotEmpty(method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: false));
        Assert.NotEmpty(method.GetCustomAttributes<EnableRateLimitingAttribute>(inherit: false));
    }
}
