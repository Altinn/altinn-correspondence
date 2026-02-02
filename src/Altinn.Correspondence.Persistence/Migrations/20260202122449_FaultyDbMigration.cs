using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FaultyDbMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration intentionally breaks backward compatibility
            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Correspondences",
                schema: "correspondence",
                newName: "StatusRenamed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert the column rename
            migrationBuilder.RenameColumn(
                name: "StatusRenamed",
                table: "Correspondences",
                schema: "correspondence",
                newName: "Status");
        }
    }
}
