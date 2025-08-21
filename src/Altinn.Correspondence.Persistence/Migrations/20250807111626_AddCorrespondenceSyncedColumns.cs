using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrespondenceSyncedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SyncedFromAltinn2",
                schema: "correspondence",
                table: "CorrespondenceStatuses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SyncedFromAltinn2",
                schema: "correspondence",
                table: "CorrespondenceNotifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SyncedFromAltinn2",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SyncedFromAltinn2",
                schema: "correspondence",
                table: "CorrespondenceStatuses");

            migrationBuilder.DropColumn(
                name: "SyncedFromAltinn2",
                schema: "correspondence",
                table: "CorrespondenceNotifications");

            migrationBuilder.DropColumn(
                name: "SyncedFromAltinn2",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents");
        }
    }
}
