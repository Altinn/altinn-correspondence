using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_Forwarding_And_ForwardingEmailIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowForwarding",
                schema: "correspondence",
                table: "Correspondences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "ForwardedByUserId",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<Guid>(
                name: "NotificationShipmentId",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceForwardingEvents_CorrespondenceId_ForwardedToEmailAddress_Unique",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents",
                columns: new[] { "CorrespondenceId", "ForwardedToEmailAddress" },
                unique: true,
                filter: "\"ForwardedToEmailAddress\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CorrespondenceForwardingEvents_CorrespondenceId_ForwardedToEmailAddress_Unique",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents");

            migrationBuilder.DropColumn(
                name: "AllowForwarding",
                schema: "correspondence",
                table: "Correspondences");

            migrationBuilder.DropColumn(
                name: "NotificationShipmentId",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents");

            migrationBuilder.AlterColumn<int>(
                name: "ForwardedByUserId",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
