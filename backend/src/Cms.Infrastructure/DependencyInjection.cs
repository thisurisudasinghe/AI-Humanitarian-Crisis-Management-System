using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Application.Interfaces;
using Cms.Infrastructure.Authentication;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Http;
using Cms.Infrastructure.Security;
using Cms.Infrastructure.Services;
using Cms.Infrastructure.Stubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cms.Infrastructure;

/// <summary>
/// Registers the platform foundation: database, current user, audit trail,
/// generic repository, resilient HTTP client and fail-closed stubs for every
/// contract that another member still has to implement.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. " +
                "Set it in appsettings.Development.json or via the " +
                "ConnectionStrings__DefaultConnection environment variable.");
        }

        services.AddHttpContextAccessor();

        services.AddDbContext<CmsDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

        services.TryAddScoped<IDateTimeService, DateTimeService>();
        services.TryAddScoped<IUnitOfWork, UnitOfWork>();
        services.TryAddScoped<ICurrentUser, CurrentUser>();
        services.TryAddScoped<IAuditLogger, AuditLogger>();
        services.TryAddScoped<JwtTokenService>();
        services.TryAddScoped<EmailProtectionService>();
        services.TryAddScoped<PasswordHashingService>();
        services.TryAddScoped<IAuthenticationService, AuthenticationService>();
        services.TryAddScoped<IAuditQueryService, AuditQueryService>();
        services.TryAddScoped<IAdminService, AdminService>();
        services.TryAddScoped<IKpiService, KpiService>();
        services.TryAddScoped<IDonationService, DonationService>();
        services.TryAddScoped(typeof(IRepository<>), typeof(EfRepository<>));

        services.AddCmsHttpClient();

        // Fail-closed defaults. A module's real implementation overrides these
        // because it registers after the foundation.
        services.TryAddScoped<IOutbox, NullOutbox>();
        services.TryAddScoped<IRequestReader, NullRequestReader>();
        services.TryAddScoped<IRequestStatusService, NoOpRequestStatusService>();
        services.TryAddScoped<IRecommendationService, NullRecommendationService>();
        services.TryAddScoped<IDecisionGate, DenyAllDecisionGate>();
        services.TryAddScoped<IGeoService, HaversineOnlyGeoService>();
        services.TryAddScoped<IResourceMatcher, EmptyResourceMatcher>();
        services.TryAddScoped<IShelterRecommender, EmptyShelterRecommender>();
        services.TryAddScoped<IVolunteerMatcher, EmptyVolunteerMatcher>();

        return services;
    }
}
