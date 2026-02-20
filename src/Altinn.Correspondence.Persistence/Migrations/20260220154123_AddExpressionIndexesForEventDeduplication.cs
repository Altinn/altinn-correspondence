using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpressionIndexesForEventDeduplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceStatuses_Unique",
                schema: "correspondence",
                table: "CorrespondenceStatuses",
                columns: new[] { "CorrespondenceId", "Status", "StatusChanged", "PartyUuid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceNotifications_Unique",
                schema: "correspondence",
                table: "CorrespondenceNotifications",
                columns: new[] { "CorrespondenceId", "NotificationAddress", "NotificationChannel", "NotificationSent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceForwardingEvents_Unique",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents",
                columns: new[] { "CorrespondenceId", "ForwardedOnDate", "ForwardedByPartyUuid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceDeleteEvents_Unique",
                schema: "correspondence",
                table: "CorrespondenceDeleteEvents",
                columns: new[] { "CorrespondenceId", "EventType", "EventOccurred", "PartyUuid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the expression indexes (FK indexes remain)
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS correspondence.""IX_CorrespondenceStatuses_Unique"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS correspondence.""IX_CorrespondenceDeleteEvents_Unique"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS correspondence.""IX_CorrespondenceNotifications_Unique"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS correspondence.""IX_CorrespondenceForwardingEvents_Unique"";");
        }
    }
}
