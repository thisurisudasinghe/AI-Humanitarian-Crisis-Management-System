using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Data;

/// <summary>
/// The single EF Core context for the platform. Table and column names are snake_case
/// so the runtime model matches the hand-written SQL scripts in /db (decision D-01).
/// </summary>
public class CmsDbContext : DbContext
{
    public CmsDbContext(DbContextOptions<CmsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organisation> Organisations => Set<Organisation>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    // Owned by Member 3 (notif schema). Kept here so IOutbox can add rows
    // to the caller's SaveChanges.
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<Notification> Notifications => Set<Notification>();

    // Donation management (don schema), owned by Dev 1 (M8, P2).
    public DbSet<Donor> Donors => Set<Donor>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();

    public DbSet<Donation> Donations => Set<Donation>();

    public DbSet<DonationAllocation> DonationAllocations => Set<DonationAllocation>();

    public DbSet<CampaignExpense> CampaignExpenses => Set<CampaignExpense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CmsDbContext).Assembly);

        NamingConventions.ApplySnakeCase(modelBuilder);
    }

    public override int SaveChanges()
    {
        GuardAppendOnlyEntities();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardAppendOnlyEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        GuardAppendOnlyEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        GuardAppendOnlyEntities();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void GuardAppendOnlyEntities()
    {
        foreach (var entry in ChangeTracker.Entries<AuditEvent>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "Audit events are append-only and cannot be modified or deleted.");
            }
        }
    }
}
