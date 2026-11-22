using Cms.Application.Contracts;
using Cms.Infrastructure;
using Cms.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cms.Tests.Integration;

public sealed class NotificationSenderRegistrationTests
{
    [Fact]
    public void Configured_smtp_host_resolves_the_smtp_sender()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=unused-for-di-test",
                ["EmailProtection:EncryptionKey"] = TestSecrets.EmailEncryptionKey,
                ["EmailProtection:HashKey"] = TestSecrets.EmailHashKey,
                ["Smtp:Host"] = "smtp.example.test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddFoundation(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();
        Assert.IsType<SmtpNotificationSender>(sender);
    }
}
