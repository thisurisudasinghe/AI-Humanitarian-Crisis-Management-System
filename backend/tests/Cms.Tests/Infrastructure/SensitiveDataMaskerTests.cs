using Cms.Infrastructure.Security;

namespace Cms.Tests.Infrastructure;

public class SensitiveDataMaskerTests
{
    [Fact]
    public void MaskObject_masks_sensitive_properties_recursively()
    {
        var details = new
        {
            Email = "citizen@example.org",
            Nested = new
            {
                PhoneNumber = "+94771234567",
                CaseId = "keep-me"
            },
            Items = new[]
            {
                new { Password = "P@ssw0rd!", Id = 7 }
            }
        };

        var masked = SensitiveDataMasker.MaskObject(details);

        Assert.NotNull(masked);
        Assert.DoesNotContain("citizen@example.org", masked);
        Assert.DoesNotContain("+94771234567", masked);
        Assert.DoesNotContain("P@ssw0rd!", masked);
        Assert.Contains("keep-me", masked);
        Assert.Contains("\"id\":7", masked);
        Assert.Contains(SensitiveDataMasker.Redacted, masked);
    }

    [Fact]
    public void MaskObject_returns_null_for_null_details()
    {
        Assert.Null(SensitiveDataMasker.MaskObject(null));
    }

    [Fact]
    public void MaskJson_masks_already_serialized_payloads()
    {
        var masked = SensitiveDataMasker.MaskJson(
            """{"accessToken":"abc.def.ghi","filePath":"C:\\temp\\scan.pdf","Action":"User.LoggedIn"}""");

        Assert.NotNull(masked);
        Assert.DoesNotContain("abc.def.ghi", masked);
        Assert.DoesNotContain("scan.pdf", masked);
        Assert.Contains("User.LoggedIn", masked);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("Password")]
    [InlineData("access_token")]
    [InlineData("medicalNotes")]
    [InlineData("AccessibilityNeeds")]
    [InlineData("storagePath")]
    public void IsSensitiveKey_recognises_sensitive_names(string key)
    {
        Assert.True(SensitiveDataMasker.IsSensitiveKey(key));
    }

    [Theory]
    [InlineData("Action")]
    [InlineData("EntityType")]
    [InlineData("status")]
    public void IsSensitiveKey_leaves_normal_names_alone(string key)
    {
        Assert.False(SensitiveDataMasker.IsSensitiveKey(key));
    }
}
