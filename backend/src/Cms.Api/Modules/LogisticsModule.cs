using Cms.Application.Contracts;
using Cms.Application.Operations;
using Cms.Infrastructure.Http;
using Cms.Infrastructure.Operations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Http;
using OperationsContracts = Cms.Application.Contracts.Operations;

namespace Cms.Api.Modules;

/// <summary>Registers Member 4 operations services, matching and mapping adapters.</summary>
public static class LogisticsModule
{
    private const string RequiredUserAgent = "CMS-Humanitarian/1.0 (cms-team@example.org)";

    public static IServiceCollection AddLogisticsModule(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddCmsHttpClient("nominatim", "https://nominatim.openstreetmap.org/");
        services.AddCmsHttpClient("osrm", "https://router.project-osrm.org/");

        // Member 4 owns the user agent for its two external mobility providers.
        foreach (var name in new[] { "nominatim", "osrm" })
        {
            services.Configure<HttpClientFactoryOptions>(name, options =>
                options.HttpClientActions.Add(client =>
                {
                    client.DefaultRequestHeaders.UserAgent.Clear();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(RequiredUserAgent);
                }));
        }

        services.AddScoped<OperationsContracts.IGeoService, GeoService>();
        services.AddScoped<OperationsContracts.IResourceMatcher, ResourceMatcher>();
        services.AddScoped<OperationsContracts.IShelterRecommender, ShelterRecommender>();
        services.AddScoped<OperationsContracts.IVolunteerMatcher, VolunteerMatcher>();

        // Replace the fail-closed foundation stubs with the real Member 4 adapters.
        services.AddScoped<IGeoService>(sp =>
            new FrozenGeoServiceAdapter(sp.GetRequiredService<OperationsContracts.IGeoService>()));
        services.AddScoped<IResourceMatcher>(sp =>
            new FrozenResourceMatcherAdapter(sp.GetRequiredService<OperationsContracts.IResourceMatcher>()));
        services.AddScoped<IShelterRecommender>(sp =>
            new FrozenShelterRecommenderAdapter(sp.GetRequiredService<OperationsContracts.IShelterRecommender>()));
        services.AddScoped<IVolunteerMatcher>(sp =>
            new FrozenVolunteerMatcherAdapter(sp.GetRequiredService<OperationsContracts.IVolunteerMatcher>()));
        services.AddScoped<IOperationsService, OperationsService>();
        return services;
    }
}
