using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorrespondenceIdStatusIndexOnCorrespondenceStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CorrespondenceStatuses_CorrespondenceId",
                schema: "correspondence",
                table: "CorrespondenceStatuses");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceStatuses_CorrespondenceId_Status",
                schema: "correspondence",
                table: "CorrespondenceStatuses",
                columns: new[] { "CorrespondenceId", "Status" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CorrespondenceStatuses_CorrespondenceId_Status",
                schema: "correspondence",
                table: "CorrespondenceStatuses");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceStatuses_CorrespondenceId",
                schema: "correspondence",
                table: "CorrespondenceStatuses",
                column: "CorrespondenceId");
        }
    }
}
