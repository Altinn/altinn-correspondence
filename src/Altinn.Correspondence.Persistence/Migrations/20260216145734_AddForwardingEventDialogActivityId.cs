using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForwardingEventDialogActivityId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowSystemDeleteAfter",
                schema: "correspondence",
                table: "Correspondences");

            migrationBuilder.AddColumn<Guid>(
                name: "DialogActivityId",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DialogActivityId",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AllowSystemDeleteAfter",
                schema: "correspondence",
                table: "Correspondences",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
