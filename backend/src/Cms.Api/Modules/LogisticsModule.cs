using Cms.Application.Contracts.Operations;
using Cms.Infrastructure.Services.Operations;

namespace Cms.Api.Modules;

/// <summary>
/// Member 4 — resources, logistics, shelters, volunteers and mapping.
/// Register real implementations here as they land.
/// </summary>
public static class LogisticsModule
{
    public static IServiceCollection AddLogisticsModule(this IServiceCollection services)
    {
        services.AddScoped<IResourceMatchScoringStrategy, WeightedResourceMatchScoringStrategy>();
        return services;
    }
}
