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
            // by dropping a column that old code expects to exist
            migrationBuilder.DropColumn(
                name: "Published",
                table: "Correspondences",
                schema: "correspondence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the dropped column
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Published",
                table: "Correspondences",
                schema: "correspondence",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
