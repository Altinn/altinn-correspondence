using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Expand_ForwardingEvent_Table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NotificationShipmentId",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents",
                type: "uuid",
                nullable: false);

            migrationBuilder.AlterColumn<int>(
                name: "ForwardedByUserId",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                oldClrType: typeof(int?),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
