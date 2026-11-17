using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Cms.Api.Authentication;
using Cms.Api.Middleware;
using Cms.Api.Modules;
using Cms.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Secrets never live in source control. Environment variables win over appsettings.
builder.Configuration["EmailProtection:EncryptionKey"] =
    Environment.GetEnvironmentVariable("EMAIL_ENCRYPTION_KEY")
    ?? builder.Configuration["EmailProtection:EncryptionKey"];

builder.Configuration["EmailProtection:HashKey"] =
    Environment.GetEnvironmentVariable("EMAIL_HASH_KEY")
    ?? builder.Configuration["EmailProtection:HashKey"];

builder.Configuration["Jwt:SecretKey"] =
    Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["Jwt:SecretKey"];

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

// Authentication and authorisation (JWT bearer, role claim "role").
builder.Services.AddJwtAuthentication(builder.Configuration);

// Every feature module registers itself here.
builder.Services.AddCmsModules();

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

    // Stricter policy for credential endpoints. Apply with
    // [EnableRateLimiting("auth")] on /api/v1/auth/* controllers.
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
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
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
