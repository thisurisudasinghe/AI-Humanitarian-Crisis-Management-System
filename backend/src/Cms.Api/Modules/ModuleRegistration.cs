namespace Cms.Api.Modules;

/// <summary>
/// Single composition point for every feature module. Program.cs stays stable
/// while module owners add their own registration extensions here.
/// </summary>
public static class ModuleRegistration
{
    public static IServiceCollection AddCmsModules(this IServiceCollection services)
    {
        // Order matters only for overrides: each module registers after the
        // foundation, so its real implementation replaces the fail-closed stub.
        services.AddIdentityModule();
        services.AddCasesModule();
        services.AddAiModule();
        services.AddLogisticsModule();

        return services;
    }

    public static WebApplication MapCmsModules(this WebApplication app)
    {
        // Module-specific endpoints are mapped by their controllers.
        return app;
    }
}
