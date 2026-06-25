using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationCleanupIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create index for cleanup job performance on notifications with Altinn2 sync data
            // This index optimizes GetCorrespondencesWithSyncedNotifications query
            // which processes 16M+ notifications from a 918M row table
            //
            // The composite (NotificationSent, Id) index handles:
            // 1. Timestamp-based filtering (NotificationSent < @timestamp)
            // 2. Composite cursor pagination (NotificationSent = @timestamp AND Id < @id)
            // 3. Sorting by both columns (ORDER BY NotificationSent DESC, Id DESC)
            //
            // Note: On large production tables, consider creating index manually with CONCURRENTLY:
            //   CREATE INDEX CONCURRENTLY IX_CorrespondenceNotifications_Cleanup 
            //   ON correspondence."CorrespondenceNotifications" ("NotificationSent" DESC, "Id" DESC)
            //   WHERE "Altinn2NotificationId" IS NOT NULL AND "SyncedFromAltinn2" IS NOT NULL;

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceNotifications_Cleanup",
                schema: "correspondence",
                table: "CorrespondenceNotifications",
                columns: new[] { "NotificationSent", "Id" },
                descending: new[] { true, true },
                filter: "\"Altinn2NotificationId\" IS NOT NULL AND \"SyncedFromAltinn2\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CorrespondenceNotifications_Cleanup",
                schema: "correspondence",
                table: "CorrespondenceNotifications");
        }
    }
}
