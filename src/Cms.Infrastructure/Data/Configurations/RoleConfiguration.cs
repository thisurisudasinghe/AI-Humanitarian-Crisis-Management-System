using Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(r => r.Name)
            .IsUnique();

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        var seedDate = new DateTime(
            2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new Role
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Name = "Citizen",
                Description = "Affected person or citizen",
                CreatedAt = seedDate
            },
            new Role
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
                Name = "EmergencyCoordinator",
                Description = "Emergency coordination and crisis approvals",
                CreatedAt = seedDate
            },
            new Role
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
                Name = "CaseManager",
                Description = "Incident and case management",
                CreatedAt = seedDate
            },
            new Role
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000004"),
                Name = "LogisticsManager",
                Description = "Resource and logistics management",
                CreatedAt = seedDate
            },
            new Role
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000005"),
                Name = "ShelterAdministrator",
                Description = "Shelter operations and capacity management",
                CreatedAt = seedDate
            },
            new Role
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000006"),
                Name = "VolunteerCoordinator",
                Description = "Volunteer verification and coordination",
                CreatedAt = seedDate
            },
            new Role
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000007"),
                Name = "DonorOfficer",
                Description = "Donor and funding management",
                CreatedAt = seedDate
            },
            new Role
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000008"),
                Name = "PartnerOrganisationOfficer",
                Description = "Partner organisation operations",
                CreatedAt = seedDate
            },
            new Role
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000009"),
                Name = "SystemAdministrator",
                Description = "Platform administration",
                CreatedAt = seedDate
            },
            new Role
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000010"),
                Name = "Volunteer",
                Description = "Volunteer task participation",
                CreatedAt = seedDate
            }
        );
    }
}