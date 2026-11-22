using Cms.Domain.Entities.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations.Operations;

/// <summary>
/// Maps the Member 4 operations entities to the schemas created by the
/// idempotent SQL scripts in backend/db. Column names are finalised by
/// <see cref="NamingConventions.ApplySnakeCase"/>.
/// </summary>
internal sealed class ResourceTypeConfiguration : IEntityTypeConfiguration<ResourceType>
{
    public void Configure(EntityTypeBuilder<ResourceType> builder) =>
        builder.ToTable("resource_types", "res");
}

internal sealed class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder) =>
        builder.ToTable("inventories", "res");
}

internal sealed class AllocationConfiguration : IEntityTypeConfiguration<Allocation>
{
    public void Configure(EntityTypeBuilder<Allocation> builder) =>
        builder.ToTable("allocations", "res");
}

internal sealed class DispatchConfiguration : IEntityTypeConfiguration<Dispatch>
{
    public void Configure(EntityTypeBuilder<Dispatch> builder) =>
        builder.ToTable("dispatches", "res");
}

internal sealed class AssistanceRequestConfiguration : IEntityTypeConfiguration<AssistanceRequest>
{
    public void Configure(EntityTypeBuilder<AssistanceRequest> builder) =>
        builder.ToTable("assistance_requests", "res");
}

internal sealed class ShelterConfiguration : IEntityTypeConfiguration<Shelter>
{
    public void Configure(EntityTypeBuilder<Shelter> builder) =>
        builder.ToTable("shelters", "shelter");
}

internal sealed class ShelterAllocationConfiguration : IEntityTypeConfiguration<ShelterAllocation>
{
    public void Configure(EntityTypeBuilder<ShelterAllocation> builder) =>
        builder.ToTable("shelter_allocations", "shelter");
}

internal sealed class VolunteerConfiguration : IEntityTypeConfiguration<Volunteer>
{
    public void Configure(EntityTypeBuilder<Volunteer> builder) =>
        builder.ToTable("volunteers", "vol");
}

internal sealed class VolunteerAvailabilityConfiguration : IEntityTypeConfiguration<VolunteerAvailability>
{
    public void Configure(EntityTypeBuilder<VolunteerAvailability> builder) =>
        builder.ToTable("availability", "vol");
}

internal sealed class VolunteerTaskConfiguration : IEntityTypeConfiguration<VolunteerTask>
{
    public void Configure(EntityTypeBuilder<VolunteerTask> builder) =>
        builder.ToTable("tasks", "vol");
}

internal sealed class VolunteerActivityConfiguration : IEntityTypeConfiguration<VolunteerActivity>
{
    public void Configure(EntityTypeBuilder<VolunteerActivity> builder) =>
        builder.ToTable("activities", "vol");
}
