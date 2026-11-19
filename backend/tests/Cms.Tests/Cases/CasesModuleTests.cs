using Cms.Application.Cases.Interfaces;
using Cms.Application.DTOs;
using Cms.Application.Interfaces;
using ICurrentUser = Cms.Application.Contracts.ICurrentUser;
using Cms.Infrastructure.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.Tests.Cases;

public class CasesModuleTests
{
    [Fact]
    public void AddCasesModule_registers_real_reader_after_stub_and_all_case_services()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"cms-module-tests-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cases:UploadRoot"] = Path.Combine(temp, "uploads"),
                ["Cases:DataProtectionKeyRing"] = Path.Combine(temp, "keys")
            })
            .Build();
        var services = new ServiceCollection();
        services.AddDbContext<CmsDbContext>(options =>
            options.UseInMemoryDatabase($"cases-module-{Guid.NewGuid():N}"));
        services.AddScoped<ICurrentUser, FakeCurrentUser>();
        services.AddScoped<IDateTimeService>(_ => new FixedDateTimeService(DateTime.UtcNow));
        services.AddScoped<IRequestReader, StubRequestReader>();

        services.AddCasesModule(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<RequestReader>(scope.ServiceProvider.GetRequiredService<IRequestReader>());
        Assert.IsType<RequestStatusService>(scope.ServiceProvider.GetRequiredService<IRequestStatusService>());
        Assert.IsType<RequestService>(scope.ServiceProvider.GetRequiredService<IRequestService>());
        Assert.IsType<CrisisService>(scope.ServiceProvider.GetRequiredService<ICrisisService>());
    }

    private sealed class StubRequestReader : IRequestReader
    {
        public Task<RequestSummaryDto?> GetAsync(
            Guid requestId,
            CancellationToken cancellationToken = default) => Task.FromResult<RequestSummaryDto?>(null);
    }
}
