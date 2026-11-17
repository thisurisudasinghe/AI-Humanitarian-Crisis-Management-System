using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cms.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Member3NotificationIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "notif",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IdempotencyKey",
                schema: "notif",
                table: "Notifications",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_IdempotencyKey",
                schema: "notif",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "notif",
                table: "Notifications");
        }
    }
}
