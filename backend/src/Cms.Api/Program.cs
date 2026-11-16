using Cms.Api.Authentication;
using Cms.Application.Interfaces;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services;
using Cms.Infrastructure.Authentication;
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
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<Cms.Infrastructure.Security.EmailProtectionService>();
builder.Services.AddScoped<Cms.Infrastructure.Security.PasswordHashingService>();

// Register JWT Authentication and Authorization
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