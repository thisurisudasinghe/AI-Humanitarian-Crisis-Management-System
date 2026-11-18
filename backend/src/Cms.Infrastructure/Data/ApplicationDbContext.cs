
using Cms.Domain.Entities;
using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    public DbSet<DecisionRecord> DecisionRecords => Set<DecisionRecord>();

    public DbSet<Request> Requests => Set<Request>();

    public DbSet<Announcement> Announcements => Set<Announcement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
    }
}
