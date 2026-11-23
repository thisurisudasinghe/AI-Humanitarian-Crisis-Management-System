using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Cms.Api.Authentication;
using Cms.Api.Middleware;
using Cms.Api.Modules;
using Cms.Api.Seeding;
using Cms.Infrastructure;
using Cms.Infrastructure.Notifications;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();

static string GenerateKey() =>
    Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

// Environment variables win over appsettings/user-secrets. In Development only,
// missing keys become ephemeral process keys so a fresh checkout still runs.
builder.Configuration["EmailProtection:EncryptionKey"] =
    Environment.GetEnvironmentVariable("EMAIL_ENCRYPTION_KEY")
    ?? builder.Configuration["EmailProtection:EncryptionKey"];

builder.Configuration["EmailProtection:HashKey"] =
    Environment.GetEnvironmentVariable("EMAIL_HASH_KEY")
    ?? builder.Configuration["EmailProtection:HashKey"];

builder.Configuration["Jwt:SecretKey"] =
    Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["Jwt:SecretKey"];

builder.Configuration["ConnectionStrings:DefaultConnection"] =
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? builder.Configuration["ConnectionStrings:DefaultConnection"];

// Resend credentials are server-side only and are never read by the frontend.
builder.Configuration["Resend:ApiKey"] =
    Environment.GetEnvironmentVariable("RESEND_API_KEY")
    ?? builder.Configuration["Resend:ApiKey"];

builder.Configuration["Resend:FromEmail"] =
    Environment.GetEnvironmentVariable("RESEND_FROM_EMAIL")
    ?? builder.Configuration["Resend:FromEmail"];

builder.Configuration["Resend:FromName"] =
    Environment.GetEnvironmentVariable("RESEND_FROM_NAME")
    ?? builder.Configuration["Resend:FromName"];

builder.Configuration["EmailVerification:BaseUrl"] =
    Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_BASE_URL")
    ?? builder.Configuration["EmailVerification:BaseUrl"];

builder.Configuration["EmailVerification:LogoUrl"] =
    Environment.GetEnvironmentVariable("EMAIL_LOGO_URL")
    ?? builder.Configuration["EmailVerification:LogoUrl"];

if (isDevelopment)
{
    builder.Configuration["EmailProtection:EncryptionKey"] ??= GenerateKey();
    builder.Configuration["EmailProtection:HashKey"] ??= GenerateKey();
    builder.Configuration["Jwt:SecretKey"] ??= GenerateKey();
    builder.Configuration["ConnectionStrings:DefaultConnection"] ??=
        "Server=(localdb)\\mssqllocaldb;Database=CmsDb;Trusted_Connection=True;TrustServerCertificate=True;";

    // Development-only HTTPS defaults. Production must supply these explicitly
    // through the environment variables above; sending then fails closed.
    builder.Configuration["Resend:FromEmail"] ??= "no-reply@safeway.local";
    builder.Configuration["Resend:FromName"] ??= "Safeway";
    builder.Configuration["EmailVerification:BaseUrl"] ??= "https://localhost:3000/verify-email/confirm";
    builder.Configuration["EmailVerification:LogoUrl"] ??=
        "https://localhost:3000/safeway-email-logo.svg";
}
else
{
    var missingSecrets = new List<string>();

    if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:SecretKey"]))
    {
        missingSecrets.Add("JWT_SECRET");
    }

    if (string.IsNullOrWhiteSpace(builder.Configuration["EmailProtection:EncryptionKey"]))
    {
        missingSecrets.Add("EMAIL_ENCRYPTION_KEY");
    }

    if (string.IsNullOrWhiteSpace(builder.Configuration["EmailProtection:HashKey"]))
    {
        missingSecrets.Add("EMAIL_HASH_KEY");
    }

    if (string.IsNullOrWhiteSpace(builder.Configuration["ConnectionStrings:DefaultConnection"]))
    {
        missingSecrets.Add("ConnectionStrings__DefaultConnection");
    }

    if (missingSecrets.Count > 0)
    {
        throw new InvalidOperationException(
            $"Missing required configuration outside Development: {string.Join(", ", missingSecrets)}");
    }
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = RequestBodyLimitMiddleware.MaxBodyBytes;
});

builder.Services.AddControllers(options =>
{
    options.Filters.Add(new ProducesAttribute("application/json"));
})
.AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
.ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = ValidationProblemResponse.Create;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AI-Powered Humanitarian Crisis Management API",
        Version = "v1",
        Description = "Crisis response, aid coordination, resources, shelters and notifications."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT access token from /api/v1/auth/login."
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, "Cms.Api.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

builder.Services.AddProblemDetails();

// Platform foundation: DbContext, current user, audit trail, repository, stubs, HTTP client.
builder.Services.AddFoundation(builder.Configuration);

// Authentication and authorisation (JWT bearer, role claim "role", default deny).
builder.Services.AddJwtAuthentication(builder.Configuration);

// Every feature module registers itself here.
builder.Services.AddCmsModules(builder.Configuration);

// Development-only demo users. This keeps the documented demo accounts usable
// with the developer's own keys (user-secrets or ephemeral process keys).
if (isDevelopment)
{
    builder.Services.AddHostedService<DevelopmentDemoSeeder>();
}

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? ["http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";

        await ProblemDetailsResponses.WriteAsync(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            "Too many requests",
            "The rate limit for this endpoint has been reached. Retry after 60 seconds.",
            "RATE_LIMITED");
    };

    // Baseline protection for every endpoint: 100 requests per minute per client IP.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    // Stricter policy for credential endpoints.
    options.AddPolicy("auth", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    // Verification links are one-time credentials: stricter budgets for sending
    // and redeeming them. The global limiter still applies on top.
    var verificationPermitLimit = builder.Configuration
        .GetSection(EmailVerificationOptions.SectionName)
        .GetValue<int?>("RateLimitPermitLimit") ?? 5;

    options.AddPolicy("email-verification", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, verificationPermitLimit),
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0
        });
    });

    // Public read-only endpoints (for example health checks) get a tighter budget.
    options.AddPolicy("public", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    // Evidence uploads are heavier than JSON calls, so cap them per client IP.
    options.AddPolicy("uploads", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestBodyLimitMiddleware>();

// Turn restricted-session authorization failures into the same ProblemDetails
// contract as the rest of the API instead of an empty 403 body.
app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    var emailVerified = context.User.FindFirst("email_verified")?.Value;

    if (context.Response.StatusCode == StatusCodes.Status403Forbidden &&
        context.User.Identity?.IsAuthenticated == true &&
        string.Equals(emailVerified, "false", StringComparison.Ordinal))
    {
        await ProblemDetailsResponses.WriteAsync(
            context,
            StatusCodes.Status403Forbidden,
            "Email verification required",
            "Verify your Safeway email address before using this feature.",
            "EMAIL_VERIFICATION_REQUIRED");
    }
});

if (isDevelopment)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("Default");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapCmsModules();

app.Run();

/// <summary>Exposed so the integration test harness can reference the API assembly.</summary>
public partial class Program;
