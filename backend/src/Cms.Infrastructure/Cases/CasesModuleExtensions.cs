using Cms.Application.Cases.Common;
using Cms.Application.Cases.Interfaces;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.Infrastructure.Cases;

public static class CasesModuleExtensions
{
    /// <summary>
    /// Registers Developer 2 services. Contract request: Member 1 must call this
    /// from Cms.Api/Program.cs after the shared TryAdd* registrations.
    /// </summary>
    public static IServiceCollection AddCasesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("Cases").Get<CasesOptions>() ?? new CasesOptions();
        if (options.SlaHours <= 0)
        {
            options.SlaHours = 24;
        }

        Directory.CreateDirectory(options.UploadRoot);
        Directory.CreateDirectory(options.DataProtectionKeyRing);

        services.AddSingleton(options);
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(options.DataProtectionKeyRing))
            .SetApplicationName("Cms.Cases");

        services.AddScoped<CaseWriteHelper>();
        services.AddScoped<IOverduePolicy, OverduePolicy>();
        services.AddScoped<ISensitiveDataProtector, SensitiveDataProtector>();
        services.AddScoped<IFileStore, LocalFileStore>();
        services.AddScoped<ICloseCaseDecisionGate, UnavailableCloseCaseDecisionGate>();

        services.AddScoped<RequestStatusService>();
        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<IRequestReader, RequestReader>();
        services.AddScoped<IRequestStatusService>(sp => sp.GetRequiredService<RequestStatusService>());
        services.AddScoped<IRequestMergeService>(sp => sp.GetRequiredService<RequestStatusService>());
        services.AddScoped<IRequestDecisionTransitionService>(sp => sp.GetRequiredService<RequestStatusService>());
        services.AddScoped<IRequestWorkflowService, RequestWorkflowService>();
        services.AddScoped<IRequestService, RequestService>();
        services.AddScoped<ICrisisService, CrisisService>();
        services.AddScoped<IHouseholdService, HouseholdService>();
        services.AddScoped<IAttachmentService, AttachmentService>();

        return services;
    }
}
