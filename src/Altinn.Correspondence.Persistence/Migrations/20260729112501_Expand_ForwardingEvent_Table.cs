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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationShipmentId",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents");
        }
    }
}
