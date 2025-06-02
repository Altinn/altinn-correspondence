using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PartyId",
                schema: "correspondence",
                table: "Correspondences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_PartyId",
                schema: "correspondence",
                table: "Correspondences",
                column: "PartyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Correspondences_PartyId",
                schema: "correspondence",
                table: "Correspondences");

            migrationBuilder.DropColumn(
                name: "PartyId",
                schema: "correspondence",
                table: "Correspondences");
        }
    }
}
