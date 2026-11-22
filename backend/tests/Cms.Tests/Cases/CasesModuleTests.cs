using Cms.Application.Cases.Interfaces;
using Cms.Application.Contracts;
using Cms.Application.DTOs;
using Cms.Application.Interfaces;
using ICurrentUser = Cms.Application.Contracts.ICurrentUser;
using Cms.Infrastructure.Cases;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using IFileStore = Cms.Application.Cases.Interfaces.IFileStore;
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
        services.AddScoped<IOutbox, OutboxService>();
        services.AddScoped<IRequestReader, StubRequestReader>();

        Cms.Api.Modules.CasesModule.AddCasesModule(services, configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<RequestReader>(scope.ServiceProvider.GetRequiredService<IRequestReader>());
        Assert.IsType<RequestStatusService>(scope.ServiceProvider.GetRequiredService<IRequestStatusService>());
        Assert.IsType<RequestStatusService>(scope.ServiceProvider.GetRequiredService<IRequestMergeService>());
        Assert.IsType<RequestStatusService>(scope.ServiceProvider.GetRequiredService<IRequestDecisionTransitionService>());
        Assert.IsType<RequestWorkflowService>(scope.ServiceProvider.GetRequiredService<IRequestWorkflowService>());
        Assert.IsType<RequestService>(scope.ServiceProvider.GetRequiredService<IRequestService>());
        Assert.IsType<CrisisService>(scope.ServiceProvider.GetRequiredService<ICrisisService>());
        Assert.IsType<HouseholdService>(scope.ServiceProvider.GetRequiredService<IHouseholdService>());
        Assert.IsType<AttachmentService>(scope.ServiceProvider.GetRequiredService<IAttachmentService>());
        Assert.IsType<DecisionGateCloseCaseAdapter>(scope.ServiceProvider.GetRequiredService<ICloseCaseDecisionGate>());
        Assert.IsType<LocalFileStore>(scope.ServiceProvider.GetRequiredService<IFileStore>());
        Assert.IsType<OverduePolicy>(scope.ServiceProvider.GetRequiredService<IOverduePolicy>());
        Assert.IsType<SensitiveDataProtector>(scope.ServiceProvider.GetRequiredService<ISensitiveDataProtector>());
    }

    [Fact]
    public void AddCasesModule_allows_member3_gate_override()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"cms-gate-tests-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cases:UploadRoot"] = Path.Combine(temp, "uploads"),
                ["Cases:DataProtectionKeyRing"] = Path.Combine(temp, "keys")
            })
            .Build();
        var services = new ServiceCollection();
        services.AddScoped<ICloseCaseDecisionGate, OverrideGate>();

        Cms.Api.Modules.CasesModule.AddCasesModule(services, configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<OverrideGate>(scope.ServiceProvider.GetRequiredService<ICloseCaseDecisionGate>());
    }

    private sealed class OverrideGate : ICloseCaseDecisionGate
    {
        public Task<bool> IsApprovedAsync(
            Guid decisionId,
            Guid requestId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class StubRequestReader : IRequestReader
    {
        public Task<RequestSummaryDto?> GetAsync(
            Guid requestId,
            CancellationToken cancellationToken = default) => Task.FromResult<RequestSummaryDto?>(null);
    }
}
