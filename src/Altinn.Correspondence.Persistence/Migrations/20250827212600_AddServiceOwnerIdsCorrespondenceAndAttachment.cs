using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceOwnerIdsCorrespondenceAndAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServiceOwnerId",
                schema: "correspondence",
                table: "Correspondences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceOwnerId",
                schema: "correspondence",
                table: "Attachments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_ServiceOwnerId",
                schema: "correspondence",
                table: "Correspondences",
                column: "ServiceOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ServiceOwnerId",
                schema: "correspondence",
                table: "Attachments",
                column: "ServiceOwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Correspondences_ServiceOwnerId",
                schema: "correspondence",
                table: "Correspondences");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_ServiceOwnerId",
                schema: "correspondence",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "ServiceOwnerId",
                schema: "correspondence",
                table: "Correspondences");

            migrationBuilder.DropColumn(
                name: "ServiceOwnerId",
                schema: "correspondence",
                table: "Attachments");
        }
    }
}
