using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIndicesForPerformancet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceStatuses_Status",
                schema: "correspondence",
                table: "CorrespondenceStatuses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_Created",
                schema: "correspondence",
                table: "Correspondences",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_Recipient",
                schema: "correspondence",
                table: "Correspondences",
                column: "Recipient");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_ResourceId",
                schema: "correspondence",
                table: "Correspondences",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_Sender",
                schema: "correspondence",
                table: "Correspondences",
                column: "Sender");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CorrespondenceStatuses_Status",
                schema: "correspondence",
                table: "CorrespondenceStatuses");

            migrationBuilder.DropIndex(
                name: "IX_Correspondences_Created",
                schema: "correspondence",
                table: "Correspondences");

            migrationBuilder.DropIndex(
                name: "IX_Correspondences_Recipient",
                schema: "correspondence",
                table: "Correspondences");

            migrationBuilder.DropIndex(
                name: "IX_Correspondences_ResourceId",
                schema: "correspondence",
                table: "Correspondences");

            migrationBuilder.DropIndex(
                name: "IX_Correspondences_Sender",
                schema: "correspondence",
                table: "Correspondences");
        }
    }
}
