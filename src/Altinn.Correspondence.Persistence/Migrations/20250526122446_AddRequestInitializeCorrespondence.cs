using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestInitializeCorrespondence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add OriginalRequest column to CorrespondenceEntity
            migrationBuilder.AddColumn<string>(
                name: "OriginalRequest",
                schema: "correspondence",
                table: "Correspondences",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalRequest",
                schema: "correspondence",
                table: "Correspondences");
        }
    }
}
