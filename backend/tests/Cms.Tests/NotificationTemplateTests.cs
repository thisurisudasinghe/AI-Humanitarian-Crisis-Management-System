using Cms.Application.DTOs.Notifications;
using Cms.Infrastructure.Notifications;

namespace Cms.Tests;

public class NotificationTemplateTests
{
    [Fact]
    public void Render_UsesEnglishTemplateByDefault()
    {
        var payload = new NotificationPayloadDto
        {
            TemplateKey = "Account.Welcome",
            Data = new Dictionary<string, string> { ["name"] = "Ada" }
        };

        var (subject, body) = NotificationTemplates.Render(payload);

        Assert.Equal("Welcome to SAFEWAY", subject);
        Assert.Equal("Hello Ada, your account is ready.", body);
    }

    [Fact]
    public void Render_UsesSecondLanguageWhenRequested()
    {
        var payload = new NotificationPayloadDto
        {
            TemplateKey = "Account.Welcome",
            Language = "es",
            Data = new Dictionary<string, string> { ["name"] = "Ada" }
        };

        var (subject, body) = NotificationTemplates.Render(payload);

        Assert.Equal("Bienvenido a SAFEWAY", subject);
        Assert.Equal("Hola Ada, su cuenta está lista.", body);
    }
}
