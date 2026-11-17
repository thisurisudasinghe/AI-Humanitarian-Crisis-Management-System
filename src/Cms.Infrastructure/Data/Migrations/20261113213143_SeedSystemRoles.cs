using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Cms.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedSystemRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "Description", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Affected person or citizen", "Citizen", null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Emergency coordination and crisis approvals", "EmergencyCoordinator", null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Incident and case management", "CaseManager", null },
                    { new Guid("10000000-0000-0000-0000-000000000004"), new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Resource and logistics management", "LogisticsManager", null },
                    { new Guid("10000000-0000-0000-0000-000000000005"), new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Shelter operations and capacity management", "ShelterAdministrator", null },
                    { new Guid("10000000-0000-0000-0000-000000000006"), new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Volunteer verification and coordination", "VolunteerCoordinator", null },
                    { new Guid("10000000-0000-0000-0000-000000000007"), new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Donor and funding management", "DonorOfficer", null },
                    { new Guid("10000000-0000-0000-0000-000000000008"), new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Partner organisation operations", "PartnerOrganisationOfficer", null },
                    { new Guid("10000000-0000-0000-0000-000000000009"), new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Platform administration", "SystemAdministrator", null },
                    { new Guid("10000000-0000-0000-0000-000000000010"), new DateTime(2026, 11, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Volunteer task participation", "Volunteer", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000010"));
        }
    }
}
