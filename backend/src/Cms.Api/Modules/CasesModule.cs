using Microsoft.Extensions.Configuration;

namespace Cms.Api.Modules;

/// <summary>
/// Member 2 — crises, areas, requests, the citizen portal and the coordinator dashboard.
/// Register real implementations here as they land.
/// </summary>
public static class CasesModule
{
    public static IServiceCollection AddCasesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Delegate to the real Developer 2 registrations. Keeping this thin API
        // module lets Program.cs compose features without referencing each
        // infrastructure namespace directly.
        return Cms.Infrastructure.Cases.CasesModuleExtensions.AddCasesModule(
            services,
            configuration);
    }
}
