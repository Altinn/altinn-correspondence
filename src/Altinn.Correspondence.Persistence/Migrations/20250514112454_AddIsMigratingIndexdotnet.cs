using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsMigratingIndexdotnet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_IsMigrating",
                schema: "correspondence",
                table: "Correspondences",
                column: "IsMigrating");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Correspondences_IsMigrating",
                schema: "correspondence",
                table: "Correspondences");
        }
    }
}
