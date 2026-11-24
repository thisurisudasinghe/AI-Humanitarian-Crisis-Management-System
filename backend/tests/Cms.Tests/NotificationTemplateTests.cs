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

    [Theory]
    [InlineData("en", "Your request status has changed", "Request abc is now Verified.", "Verified")]
    [InlineData("si", "ඔබේ ඉල්ලීමේ තත්ත්වය යාවත්කාලීනයි", "abc ඉල්ලීම දැන් Verified තත්ත්වයේ පවතී.", "Verified")]
    [InlineData("ta", "உங்கள் கோரிக்கையின் நிலை மாற்றம்", "கோரிக்கை abc இப்போது Verified நிலையில் உள்ளது.", "Verified")]
    [InlineData("es", "El estado de su solicitud ha cambiado", "La solicitud abc ahora está Verified.", "Verified")]
    public void Render_uses_canonical_key_in_every_supported_language(
        string language,
        string expectedSubject,
        string expectedBody,
        string status)
    {
        var payload = new NotificationPayloadDto
        {
            TemplateKey = "Request.StatusChanged",
            Language = language,
            Data = new Dictionary<string, string>
            {
                ["requestId"] = "abc",
                ["status"] = status
            }
        };

        var (subject, body) = NotificationTemplates.Render(payload);

        Assert.Equal(expectedSubject, subject);
        Assert.Equal(expectedBody, body);
    }

    [Theory]
    [InlineData("Donation.Received", "en")]
    [InlineData("Donation.Received", "si")]
    [InlineData("Donation.Received", "ta")]
    [InlineData("Donation.Allocated", "en")]
    [InlineData("Donation.Allocated", "si")]
    [InlineData("Donation.Allocated", "ta")]
    public void Render_donation_templates_without_raw_placeholders(
        string templateKey,
        string language)
    {
        var payload = new NotificationPayloadDto
        {
            TemplateKey = templateKey,
            Language = language,
            Data = new Dictionary<string, string>
            {
                ["reference"] = "DON-20261124-ABC12345",
                ["campaign"] = "Flood relief",
                ["amount"] = "750",
                ["currency"] = "LKR"
            }
        };

        var (subject, body) = NotificationTemplates.Render(payload);

        Assert.Contains("DON-20261124-ABC12345", body);
        Assert.Contains("750", body);
        Assert.Contains("LKR", body);
        Assert.DoesNotContain("{", subject);
        Assert.DoesNotContain("}", subject);
        Assert.DoesNotContain("{", body);
        Assert.DoesNotContain("}", body);
        if (templateKey == "Donation.Allocated")
        {
            Assert.Contains("Flood relief", body);
        }
    }

    [Fact]
    public void Supported_languages_include_locales_and_spanish()
    {
        Assert.Equal(new[] { "en", "si", "ta", "es" }, NotificationTemplates.SupportedLanguages);
    }

    [Fact]
    public void Render_keeps_user_generated_announcement_text_unchanged()
    {
        const string message = "Evacuate via Gate 2";
        var payload = new NotificationPayloadDto
        {
            TemplateKey = "Announcement.Approved",
            Language = "si",
            Data = new Dictionary<string, string> { ["message"] = message }
        };

        var (_, body) = NotificationTemplates.Render(payload);

        Assert.Equal(message, body);
    }
}
