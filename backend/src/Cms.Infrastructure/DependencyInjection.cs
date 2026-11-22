using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Application.Interfaces;
using Cms.Infrastructure.Authentication;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Http;
using Cms.Infrastructure.Notifications;
using Cms.Infrastructure.Security;
using Cms.Infrastructure.Services;
using Cms.Infrastructure.Storage;
using Cms.Infrastructure.Stubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
        services.TryAddScoped<IOutbox, OutboxService>();
        services.TryAddScoped<IOutboxAdminService, OutboxAdminService>();
        services.TryAddScoped<INotificationService, NotificationService>();
        services.TryAddScoped(typeof(IRepository<>), typeof(EfRepository<>));

        // Local evidence storage. The root is configuration-driven so tests and
        // deployments can point it outside the content directory.
        services.TryAddSingleton<IFileStore>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var environment = provider.GetRequiredService<IHostEnvironment>();
            var configured = configuration["FileStorage:RootPath"];

            var root = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(environment.ContentRootPath, "App_Data", "uploads")
                : Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(environment.ContentRootPath, configured);

            return new LocalFileStore(
                root,
                provider.GetRequiredService<ILogger<LocalFileStore>>());
        });
        services.TryAddScoped<IFileService, FileService>();

        services.AddCmsHttpClient();

        // M9 notification delivery. The SMTP sender is used only when a host is
        // configured; otherwise the log sender keeps a fresh checkout working.
        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.TryAddScoped<INotificationSender>(provider =>
        {
            var options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpOptions>>()
                .Value;

            return string.IsNullOrWhiteSpace(options.Host)
                ? ActivatorUtilities.CreateInstance<LogNotificationSender>(provider)
                : ActivatorUtilities.CreateInstance<SmtpNotificationSender>(provider, options);
        });
        services.TryAddScoped<InAppNotificationChannel>();
        services.TryAddScoped<EmailNotificationChannel>();
        services.TryAddScoped<INotificationChannelFactory, NotificationChannelFactory>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IOutboxHandler, EmailNotificationHandler>());

        // Transactional outbox dispatcher. Modules register IOutboxHandler
        // implementations; the processor resolves them per message.
        services.AddHostedService<OutboxProcessor>();

        // Fail-closed defaults. A module's real implementation overrides these
        // because it registers after the foundation.
        services.TryAddScoped<IRequestReader, NullRequestReader>();
        services.TryAddScoped<IRequestStatusService, NoOpRequestStatusService>();
        services.TryAddScoped<IDecisionGate, DenyAllDecisionGate>();
        services.TryAddScoped<IGeoService, HaversineOnlyGeoService>();
        services.TryAddScoped<IResourceMatcher, EmptyResourceMatcher>();
        services.TryAddScoped<IShelterRecommender, EmptyShelterRecommender>();
        services.TryAddScoped<IVolunteerMatcher, EmptyVolunteerMatcher>();

        return services;
    }
}
