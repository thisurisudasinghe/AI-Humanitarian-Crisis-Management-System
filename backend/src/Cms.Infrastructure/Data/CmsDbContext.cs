using Cms.Domain.Entities;
using Cms.Domain.Entities.Access;
using Cms.Domain.Entities.Operations;
using Cms.Domain.Entities.Cases;
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

    public DbSet<Request> Requests => Set<Request>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    // Shared transactional outbox (common.outbox_messages), owned by Dev 1.
    // Kept on the shared context so callers can add an event row to their own
    // SaveChanges transaction.
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<Notification> Notifications => Set<Notification>();

    // Donation management (don schema), owned by Dev 1 (M8, P2).
    public DbSet<Donor> Donors => Set<Donor>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();

    public DbSet<Donation> Donations => Set<Donation>();

    public DbSet<DonationAllocation> DonationAllocations => Set<DonationAllocation>();

    public DbSet<DonationItem> DonationItems => Set<DonationItem>();

    public DbSet<CampaignExpense> CampaignExpenses => Set<CampaignExpense>();

    // AI decision support and governance (ai schema), owned by Dev 3 (M3/M4).
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    public DbSet<DecisionRecord> DecisionRecords => Set<DecisionRecord>();

    // Announcements (notif schema), owned by Dev 3 (M9). Restored after the M4 merge.
    public DbSet<Announcement> Announcements => Set<Announcement>();

    // Member 4 — operations schemas (res, shelter, vol).
    public DbSet<ResourceType> ResourceTypes => Set<ResourceType>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Allocation> Allocations => Set<Allocation>();
    public DbSet<Dispatch> Dispatches => Set<Dispatch>();
    public DbSet<AssistanceRequest> AssistanceRequests => Set<AssistanceRequest>();
    public DbSet<Shelter> Shelters => Set<Shelter>();
    public DbSet<ShelterAllocation> ShelterAllocations => Set<ShelterAllocation>();
    public DbSet<Volunteer> Volunteers => Set<Volunteer>();
    public DbSet<VolunteerAvailability> VolunteerAvailabilities => Set<VolunteerAvailability>();
    public DbSet<VolunteerTask> VolunteerTasks => Set<VolunteerTask>();
    public DbSet<VolunteerActivity> VolunteerActivities => Set<VolunteerActivity>();

    // Access and guest reporting (access schema).
    public DbSet<StaffAccessApplication> StaffAccessApplications => Set<StaffAccessApplication>();
    public DbSet<GuestReport> GuestReports => Set<GuestReport>();
    public DbSet<GuestReportTimeline> GuestReportTimelines => Set<GuestReportTimeline>();

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

        foreach (var entry in ChangeTracker.Entries<DecisionRecord>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "Decision records are immutable and cannot be modified or deleted.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<Recommendation>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "Recommendations are immutable and cannot be modified or deleted.");
            }
        }
    }
}
