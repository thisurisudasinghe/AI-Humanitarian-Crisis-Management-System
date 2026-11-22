using Cms.Application.Contracts.Operations;
using Cms.Infrastructure.Http;
using Cms.Infrastructure.Operations;
using Microsoft.Extensions.Caching.Memory;

namespace Cms.Api.Modules;

/// <summary>Registers Member 4 operations matching and mapping adapters.</summary>
public static class LogisticsModule
{
    public static IServiceCollection AddLogisticsModule(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddCmsHttpClient("nominatim", "https://nominatim.openstreetmap.org/");
        services.AddCmsHttpClient("osrm", "https://router.project-osrm.org/");
        services.AddScoped<IGeoService, GeoService>();
        services.AddScoped<IResourceMatcher, ResourceMatcher>();
        services.AddScoped<IShelterRecommender, ShelterRecommender>();
        services.AddScoped<IVolunteerMatcher, VolunteerMatcher>();
        return services;
    }
}
