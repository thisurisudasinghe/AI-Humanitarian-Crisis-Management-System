using Cms.Api.Authentication;
using Cms.Application.Interfaces;
using Cms.Application.Interfaces.AI;
using Cms.Application.Interfaces.Announcements;
using Cms.Application.Interfaces.Governance;
using Cms.Infrastructure.Authentication;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services;
using Cms.Infrastructure.Services.AI;
using Cms.Infrastructure.Services.Announcements;
using Cms.Infrastructure.Services.Governance;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);
// Map V4 environment variables to application configuration keys
builder.Configuration["EmailProtection:EncryptionKey"] =
    Environment.GetEnvironmentVariable("EMAIL_ENCRYPTION_KEY")
    ?? builder.Configuration["EmailProtection:EncryptionKey"];

builder.Configuration["EmailProtection:HashKey"] =
    Environment.GetEnvironmentVariable("EMAIL_HASH_KEY")
    ?? builder.Configuration["EmailProtection:HashKey"];

builder.Configuration["Jwt:SecretKey"] =
    Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["Jwt:SecretKey"];

// Register API services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register SQL Server Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Shared Infrastructure Services
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IDateTimeService, DateTimeService>();
builder.Services.AddScoped<IAuditService, Cms.Infrastructure.Services.AuditService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<
    INotificationService,
    Cms.Infrastructure.Services.Notifications.NotificationService>();

builder.Services.AddScoped<
    Cms.Application.Interfaces.Notifications.INotificationChannelFactory,
    Cms.Infrastructure.Services.Notifications.NotificationChannelFactory>();

builder.Services.AddScoped<Cms.Infrastructure.Services.Notifications.EmailChannel>();
builder.Services.AddScoped<Cms.Infrastructure.Services.Notifications.InAppChannel>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<Cms.Infrastructure.Security.EmailProtectionService>();
builder.Services.AddScoped<Cms.Infrastructure.Security.PasswordHashingService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

// AI decision support: optional LLM with a deterministic offline fallback.
builder.Services.AddHttpClient<LlmProvider>();
builder.Services.AddScoped<RuleBasedProvider>();
builder.Services.AddScoped<IAiProvider, AiProviderSelector>();

// Governance (human-controlled decisions).
builder.Services.AddScoped<IDecisionGate, DecisionGate>();
builder.Services.AddScoped<IDecisionService, DecisionService>();
builder.Services.AddScoped<IPostIncidentReviewService, PostIncidentReviewService>();
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();

builder.Services.AddScoped<
    IOutbox,
    Cms.Infrastructure.Services.Outbox.OutboxService>();
builder.Services.AddScoped<
    Cms.Application.Interfaces.IOutboxHandler,
    Cms.Infrastructure.Services.Outbox.OutboxHandler>();
builder.Services.AddScoped<
    Cms.Application.Interfaces.IOutboxHandler,
    RequestSubmittedHandler>();
builder.Services.AddScoped<
    Cms.Application.Interfaces.IOutboxHandlerRegistry,
    Cms.Infrastructure.Services.Outbox.OutboxHandlerRegistry>();
builder.Services.AddScoped<
    INotificationInboxService,
    Cms.Infrastructure.Services.Notifications.NotificationInboxService>();
builder.Services.AddScoped<
    Cms.Infrastructure.Services.Notifications.NotificationTemplateRenderer>();
builder.Services.AddHostedService<
    Cms.Infrastructure.Services.Outbox.OutboxProcessor>();

// Register JWT Authentication and =
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();