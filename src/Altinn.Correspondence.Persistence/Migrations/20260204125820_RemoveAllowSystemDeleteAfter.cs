using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAllowSystemDeleteAfter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowSystemDeleteAfter",
                schema: "correspondence",
                table: "Correspondences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AllowSystemDeleteAfter",
                schema: "correspondence",
                table: "Correspondences",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
