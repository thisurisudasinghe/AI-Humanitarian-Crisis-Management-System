using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cms.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Member3NotificationsOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_NextRetryAt_Failed",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_NextRetryAt_Pending",
                table: "OutboxMessages");

            migrationBuilder.EnsureSchema(
                name: "ai");

            migrationBuilder.EnsureSchema(
                name: "notif");

            migrationBuilder.RenameTable(
                name: "OutboxMessages",
                newName: "OutboxMessages",
                newSchema: "notif");

            migrationBuilder.RenameTable(
                name: "Notifications",
                newName: "Notifications",
                newSchema: "notif");

            migrationBuilder.RenameColumn(
                name: "Payload",
                schema: "notif",
                table: "OutboxMessages",
                newName: "PayloadJson");

            migrationBuilder.RenameColumn(
                name: "NextRetryAt",
                schema: "notif",
                table: "OutboxMessages",
                newName: "NextAttemptAt");

            migrationBuilder.RenameColumn(
                name: "ErrorMessage",
                schema: "notif",
                table: "OutboxMessages",
                newName: "LastError");

            migrationBuilder.RenameColumn(
                name: "RetryCount",
                schema: "notif",
                table: "OutboxMessages",
                newName: "Attempts");

            migrationBuilder.AddColumn<Guid>(
                name: "EntityId",
                schema: "notif",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "notif",
                table: "OutboxMessages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Recommendations",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recommendations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DecisionRecords",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecommendationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FinalPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DecisionRecords_Recommendations_RecommendationId",
                        column: x => x.RecommendationId,
                        principalSchema: "ai",
                        principalTable: "Recommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_IdempotencyKey",
                schema: "notif",
                table: "OutboxMessages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAt",
                schema: "notif",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionRecords_RecommendationId",
                schema: "ai",
                table: "DecisionRecords",
                column: "RecommendationId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_TargetId_Type",
                schema: "ai",
                table: "Recommendations",
                columns: new[] { "TargetId", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DecisionRecords",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "Recommendations",
                schema: "ai");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_IdempotencyKey",
                schema: "notif",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAt",
                schema: "notif",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "EntityId",
                schema: "notif",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "notif",
                table: "OutboxMessages");

            migrationBuilder.RenameTable(
                name: "OutboxMessages",
                schema: "notif",
                newName: "OutboxMessages");

            migrationBuilder.RenameTable(
                name: "Notifications",
                schema: "notif",
                newName: "Notifications");

            migrationBuilder.RenameColumn(
                name: "PayloadJson",
                table: "OutboxMessages",
                newName: "Payload");

            migrationBuilder.RenameColumn(
                name: "NextAttemptAt",
                table: "OutboxMessages",
                newName: "NextRetryAt");

            migrationBuilder.RenameColumn(
                name: "LastError",
                table: "OutboxMessages",
                newName: "ErrorMessage");

            migrationBuilder.RenameColumn(
                name: "Attempts",
                table: "OutboxMessages",
                newName: "RetryCount");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextRetryAt_Failed",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextRetryAt" },
                filter: "[Status] = 'Failed'");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextRetryAt_Pending",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextRetryAt" },
                filter: "[Status] = 'Pending'");
            migrationBuilder.Sql(
    """
    UPDATE [notif].[OutboxMessages]
    SET [IdempotencyKey] = CONVERT(nvarchar(36), NEWID())
    WHERE [IdempotencyKey] = '';
    """);
        }
    }
}
