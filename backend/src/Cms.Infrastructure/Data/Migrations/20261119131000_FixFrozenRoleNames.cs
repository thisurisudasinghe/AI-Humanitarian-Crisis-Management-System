using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cms.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixFrozenRoleNames : Migration
    {
        private const string ShelterRoleId = "10000000-0000-0000-0000-000000000005";
        private const string PartnerRoleId = "10000000-0000-0000-0000-000000000008";
        private const string AdminRoleId = "10000000-0000-0000-0000-000000000009";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data-only, in-place rename. UserRole rows keep the same RoleId FK.
            migrationBuilder.Sql(
                $"UPDATE [Roles] SET [Name] = N'ShelterAdmin' WHERE [Id] = '{ShelterRoleId}';");
            migrationBuilder.Sql(
                $"UPDATE [Roles] SET [Name] = N'PartnerOfficer' WHERE [Id] = '{PartnerRoleId}';");
            migrationBuilder.Sql(
                $"UPDATE [Roles] SET [Name] = N'SystemAdmin' WHERE [Id] = '{AdminRoleId}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE [Roles] SET [Name] = N'ShelterAdministrator' WHERE [Id] = '{ShelterRoleId}';");
            migrationBuilder.Sql(
                $"UPDATE [Roles] SET [Name] = N'PartnerOrganisationOfficer' WHERE [Id] = '{PartnerRoleId}';");
            migrationBuilder.Sql(
                $"UPDATE [Roles] SET [Name] = N'SystemAdministrator' WHERE [Id] = '{AdminRoleId}';");
        }
    }
}
