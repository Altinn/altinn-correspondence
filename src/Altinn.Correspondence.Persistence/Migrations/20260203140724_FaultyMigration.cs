using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FaultyMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Published",
                table: "Correspondences",
                schema: "correspondence");
            migrationBuilder.DropColumn(
                name: "IsMigrating",
                table: "Correspondences",
                schema: "correspondence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Published",
                table: "Correspondences",
                schema: "correspondence",
                type: "timestamp with time zone",
                nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "IsMigrating",
                table: "Correspondences",
                schema: "correspondence",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
