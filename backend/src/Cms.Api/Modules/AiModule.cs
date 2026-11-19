using Cms.Application.Interfaces.AI;
using Cms.Infrastructure.Services.AI;

namespace Cms.Api.Modules;

/// <summary>
/// Member 3 - AI decision support and governance (M3/M4).
/// Registers the real recommendation service on top of the shared
/// <c>CmsDbContext</c>; the foundation's fail-closed decision gate stays in
/// place until the governance implementation lands.
/// </summary>
public static class AiModule
{
    public static IServiceCollection AddAiModule(this IServiceCollection services)
    {
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IDecisionService, DecisionService>();

        return services;
    }
}
