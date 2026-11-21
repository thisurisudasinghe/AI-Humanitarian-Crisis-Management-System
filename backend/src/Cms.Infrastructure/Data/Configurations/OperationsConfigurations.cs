using Cms.Domain.Entities.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public sealed class ResourceTypeConfiguration : IEntityTypeConfiguration<ResourceType>
{
    public void Configure(EntityTypeBuilder<ResourceType> b) { b.ToTable("ResourceTypes", "res"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(120).IsRequired(); b.Property(x => x.Unit).HasMaxLength(40).IsRequired(); b.Property(x => x.Description).HasMaxLength(500); }
}
public sealed class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> b) { b.ToTable("Inventories", "res", t => t.HasCheckConstraint("CK_Inventories_NonNegative", "Available >= 0 AND Reserved >= 0 AND Allocated >= 0 AND Dispatched >= 0 AND Utilised >= 0")); b.HasKey(x => x.Id); b.HasIndex(x => x.OrgId); b.HasIndex(x => x.ResourceTypeId); b.Property(x => x.Quantity).HasPrecision(18,2); b.Property(x => x.Available).HasPrecision(18,2); b.Property(x => x.Reserved).HasPrecision(18,2); b.Property(x => x.Allocated).HasPrecision(18,2); b.Property(x => x.Dispatched).HasPrecision(18,2); b.Property(x => x.Utilised).HasPrecision(18,2); b.Property(x => x.ReorderThreshold).HasPrecision(18,2); }
}
public sealed class AllocationConfiguration : IEntityTypeConfiguration<Allocation>
{
    public void Configure(EntityTypeBuilder<Allocation> b) { b.ToTable("Allocations", "res"); b.HasKey(x => x.Id); b.HasIndex(x => x.IdempotencyKey).IsUnique(); b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired(); b.Property(x => x.Quantity).HasPrecision(18,2); b.Property(x => x.Status).HasMaxLength(30); b.Property(x => x.Explanation).HasMaxLength(1000); }
}
public sealed class DispatchConfiguration : IEntityTypeConfiguration<Dispatch>
{
    public void Configure(EntityTypeBuilder<Dispatch> b) { b.ToTable("Dispatches", "res"); b.HasKey(x => x.Id); b.HasIndex(x => x.AllocationId); b.Property(x => x.Quantity).HasPrecision(18,2); b.Property(x => x.Status).HasMaxLength(30); }
}
public sealed class AssistanceRequestConfiguration : IEntityTypeConfiguration<AssistanceRequest>
{
    public void Configure(EntityTypeBuilder<AssistanceRequest> b) { b.ToTable("AssistanceRequests", "res"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.FromOrgId, x.Status }); b.HasIndex(x => new { x.ToOrgId, x.Status }); b.Property(x => x.Quantity).HasPrecision(18,2); b.Property(x => x.Reason).HasMaxLength(1000); b.Property(x => x.ResponseReason).HasMaxLength(1000); b.Property(x => x.Status).HasMaxLength(20); }
}
public sealed class ShelterConfiguration : IEntityTypeConfiguration<Shelter>
{
    public void Configure(EntityTypeBuilder<Shelter> b) { b.ToTable("Shelters", "shelter", t => { t.HasCheckConstraint("CK_Shelters_Capacity", "Capacity > 0"); t.HasCheckConstraint("CK_Shelters_Occupancy", "Occupancy >= 0 AND Occupancy <= Capacity"); }); b.HasKey(x => x.Id); b.HasIndex(x => x.OrgId); b.HasIndex(x => x.Approved); b.Property(x => x.Name).HasMaxLength(160).IsRequired(); }
}
public sealed class ShelterAllocationConfiguration : IEntityTypeConfiguration<ShelterAllocation>
{
    public void Configure(EntityTypeBuilder<ShelterAllocation> b) { b.ToTable("ShelterAllocations", "shelter"); b.HasKey(x => x.Id); b.HasIndex(x => x.ShelterId); b.HasIndex(x => x.RequestId); }
}
public sealed class VolunteerConfiguration : IEntityTypeConfiguration<Volunteer>
{
    public void Configure(EntityTypeBuilder<Volunteer> b) { b.ToTable("Volunteers", "vol"); b.HasKey(x => x.Id); b.HasIndex(x => x.UserId).IsUnique(); b.Property(x => x.VerificationStatus).HasMaxLength(20); }
}
public sealed class VolunteerAvailabilityConfiguration : IEntityTypeConfiguration<VolunteerAvailability>
{
    public void Configure(EntityTypeBuilder<VolunteerAvailability> b) { b.ToTable("Availability", "vol", t => t.HasCheckConstraint("CK_VolAvailability", "EndsAt > StartsAt")); b.HasKey(x => x.Id); b.HasIndex(x => new { x.VolunteerId, x.StartsAt, x.EndsAt }); }
}
public sealed class VolunteerTaskConfiguration : IEntityTypeConfiguration<VolunteerTask>
{
    public void Configure(EntityTypeBuilder<VolunteerTask> b) { b.ToTable("Tasks", "vol"); b.HasKey(x => x.Id); b.HasIndex(x => x.Status); b.Property(x => x.Title).HasMaxLength(160).IsRequired(); b.Property(x => x.Status).HasMaxLength(20); }
}
public sealed class VolunteerActivityConfiguration : IEntityTypeConfiguration<VolunteerActivity>
{
    public void Configure(EntityTypeBuilder<VolunteerActivity> b) { b.ToTable("Activities", "vol"); b.HasKey(x => x.Id); b.HasIndex(x => x.VolunteerId); b.HasIndex(x => x.TaskId); }
}
